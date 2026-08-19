using System;
using System.Collections.Generic;
using UnityEngine;
using Complex = System.Numerics.Complex;

#if FUSION_PRESENT || FUSION_WEAVER
using Fusion;
#endif

/// <summary>Operations that can change the shared quantum state.</summary>
public enum QuantumGateOperation : byte
{
    Hadamard,
    PauliX,
    PauliZ,
    PhaseS
}

#if FUSION_PRESENT || FUSION_WEAVER
namespace ArtsOfEntanglement.Colocation
{
    /// <summary>
    /// The one authoritative owner of the quantum experiment in a Fusion room.
    /// It replicates an 8x8 density matrix (128 floats) and the three shared
    /// qubit poses. Clients request mutations but never evolve the state.
    /// </summary>
    public sealed class QuantumSessionState : NetworkBehaviour
    {
        private const int QubitCount = 3;
        private const int DensityFloatCount = 128;
        private const float PoseSendInterval = 1f / 20f;
        private const float SnapshotSendInterval = 1f / 20f;

        [Networked] public int QuantumStateVersion { get; set; }
        [Networked] public uint QuantumStateHash { get; set; }
        [Networked] public int LastEventSequence { get; set; }
        [Networked] public int LastMeasurementQubit { get; set; }
        [Networked] public int LastMeasurementOutcome { get; set; }
        [Networked] public float LastMeasurementProbability { get; set; }
        [Networked] private NetworkBool SnapshotReady { get; set; }
        [Networked] private NetworkBool PosesReady { get; set; }

        [Networked, Capacity(DensityFloatCount)]
        private NetworkArray<float> DensityData => default;

        [Networked, Capacity(QubitCount)]
        private NetworkArray<Vector3> LocalPositions => default;

        [Networked, Capacity(QubitCount)]
        private NetworkArray<Quaternion> LocalRotations => default;

        [Networked, Capacity(QubitCount)]
        private NetworkArray<int> QubitOwners => default;

        public static QuantumSessionState Instance { get; private set; }
        public static bool IsNetworkSessionActive => Instance != null && Instance.Runner != null && Instance.Runner.IsRunning;

        private readonly float[] nextPoseSendTime = new float[QubitCount];
        private readonly HashSet<string> processedRequests = new HashSet<string>();
        private int nextRequestId;
        private int lastAppliedVersion = int.MinValue;
        private bool posesInitialized;
        private float nextSnapshotPublishTime;
        private Qubit[] qubitsById;
        private SharedSpaceAligner sharedSpaceAligner;

        public override void Spawned()
        {
            Instance = this;
            sharedSpaceAligner = FindObjectOfType<SharedSpaceAligner>();
            if (HasStateAuthority)
            {
                LastMeasurementQubit = -1;
                LastMeasurementOutcome = -1;
                TryInitializeAuthoritativeState();
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            if (!TryInitializeAuthoritativeState())
            {
                return;
            }

            ApplyAuthoritativePoses();
            QubitManager.AdvanceSimulation(Runner.DeltaTime);
            PublishDensityMatrix();
        }

        public override void Render()
        {
            if (!HasStateAuthority && SnapshotReady && QuantumStateVersion != lastAppliedVersion)
            {
                ApplyRemoteDensityMatrix();
            }

            if (PosesReady)
            {
                ApplyReplicatedPoses();
            }
        }

        public static bool RequestGate(int qubitId, QuantumGateOperation operation)
        {
            if (!IsNetworkSessionActive)
            {
                return false;
            }

            if (!IsValidQubitId(qubitId))
            {
                return true;
            }

            int requestId = ++Instance.nextRequestId;
            if (Instance.HasStateAuthority)
            {
                Instance.ApplyGateRequest(qubitId, operation, requestId, Instance.Runner.LocalPlayer);
            }
            else
            {
                Instance.RPC_RequestGate(qubitId, (byte)operation, requestId);
            }
            return true;
        }

        public static bool RequestMeasurement(int qubitId)
        {
            if (!IsNetworkSessionActive)
            {
                return false;
            }

            if (!IsValidQubitId(qubitId))
            {
                return true;
            }

            int requestId = ++Instance.nextRequestId;
            if (Instance.HasStateAuthority)
            {
                Instance.ApplyMeasurementRequest(qubitId, requestId, Instance.Runner.LocalPlayer);
            }
            else
            {
                Instance.RPC_RequestMeasurement(qubitId, requestId);
            }
            return true;
        }

        public static void BeginLocalGrab(int qubitId)
        {
            if (!IsNetworkSessionActive || !IsValidQubitId(qubitId))
            {
                return;
            }

            if (Instance.HasStateAuthority)
            {
                Instance.SetGrabOwner(qubitId, Instance.Runner.LocalPlayer, true);
            }
            else
            {
                Instance.RPC_SetGrabOwner(qubitId, true);
            }
        }

        public static void EndLocalGrab(int qubitId)
        {
            if (!IsNetworkSessionActive || !IsValidQubitId(qubitId))
            {
                return;
            }

            if (Instance.HasStateAuthority)
            {
                Instance.SetGrabOwner(qubitId, Instance.Runner.LocalPlayer, false);
            }
            else
            {
                Instance.RPC_SetGrabOwner(qubitId, false);
            }
        }

        public static bool IsLocalGrabApproved(int qubitId)
        {
            return IsNetworkSessionActive && IsValidQubitId(qubitId) &&
                Instance.QubitOwners[qubitId] == Instance.Runner.LocalPlayer.PlayerId;
        }

        public static void SubmitLocalPose(int qubitId, Vector3 worldPosition, Quaternion worldRotation)
        {
            if (!IsNetworkSessionActive || !IsValidQubitId(qubitId) || !IsLocalGrabApproved(qubitId))
            {
                return;
            }

            if (Time.unscaledTime < Instance.nextPoseSendTime[qubitId])
            {
                return;
            }
            Instance.nextPoseSendTime[qubitId] = Time.unscaledTime + PoseSendInterval;

            Instance.ToSharedLocalPose(worldPosition, worldRotation, out Vector3 localPosition, out Quaternion localRotation);
            if (Instance.HasStateAuthority)
            {
                Instance.SetAuthoritativePose(qubitId, localPosition, localRotation);
            }
            else
            {
                Instance.RPC_SubmitPose(qubitId, localPosition, localRotation);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        private void RPC_RequestGate(int qubitId, byte operation, int requestId, RpcInfo info = default)
        {
            if (operation > (byte)QuantumGateOperation.PhaseS)
            {
                return;
            }
            ApplyGateRequest(qubitId, (QuantumGateOperation)operation, requestId, info.Source);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        private void RPC_RequestMeasurement(int qubitId, int requestId, RpcInfo info = default)
        {
            ApplyMeasurementRequest(qubitId, requestId, info.Source);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        private void RPC_SetGrabOwner(int qubitId, NetworkBool grabbing, RpcInfo info = default)
        {
            SetGrabOwner(qubitId, info.Source, grabbing);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_SubmitPose(int qubitId, Vector3 localPosition, Quaternion localRotation, RpcInfo info = default)
        {
            if (IsValidQubitId(qubitId) && QubitOwners[qubitId] == info.Source.PlayerId)
            {
                SetAuthoritativePose(qubitId, localPosition, localRotation);
            }
        }

        private bool TryInitializeAuthoritativeState()
        {
            if (!QubitManager.IsInitialized || QubitManager.GetQubits() != QubitCount)
            {
                return false;
            }

            if (!posesInitialized)
            {
                var qubits = FindQubitsById();
                if (qubits == null)
                {
                    return false;
                }

                for (int i = 0; i < QubitCount; i++)
                {
                    ToSharedLocalPose(qubits[i].transform.position, qubits[i].transform.rotation,
                        out Vector3 position, out Quaternion rotation);
                    SetAuthoritativePose(i, position, rotation);
                    QubitOwners.Set(i, 0);
                }
                posesInitialized = true;
                PosesReady = true;
            }

            if (!SnapshotReady)
            {
                PublishDensityMatrix(force: true);
            }
            return true;
        }

        private void ApplyGateRequest(int qubitId, QuantumGateOperation operation, int requestId, PlayerRef source)
        {
            if (!HasStateAuthority || !IsValidQubitId(qubitId) || !AcceptRequest(source, requestId) || !QubitManager.IsInitialized)
            {
                return;
            }

            QubitManager.ApplyGateLocally(qubitId, operation);
            LastEventSequence++;
            PublishDensityMatrix(force: true);
        }

        private void ApplyMeasurementRequest(int qubitId, int requestId, PlayerRef source)
        {
            if (!HasStateAuthority || !IsValidQubitId(qubitId) || !AcceptRequest(source, requestId) || !QubitManager.IsInitialized)
            {
                return;
            }

            MeasurementResult result = QubitManager.MeasureLocally(qubitId, UnityEngine.Random.value);
            LastMeasurementQubit = result.QubitId;
            LastMeasurementOutcome = result.Outcome;
            LastMeasurementProbability = (float)result.Probability;
            LastEventSequence++;
            Debug.Log($"[QuantumSessionState] Q{result.QubitId} measured {result.Outcome} (p={result.Probability:F4}).");
            PublishDensityMatrix(force: true);
        }

        private bool AcceptRequest(PlayerRef source, int requestId)
        {
            string key = source.PlayerId + ":" + requestId;
            return processedRequests.Add(key);
        }

        private void SetGrabOwner(int qubitId, PlayerRef player, bool grabbing)
        {
            if (!HasStateAuthority || !IsValidQubitId(qubitId))
            {
                return;
            }

            int currentOwner = QubitOwners[qubitId];
            if (grabbing)
            {
                if (currentOwner == 0 || currentOwner == player.PlayerId)
                {
                    QubitOwners.Set(qubitId, player.PlayerId);
                }
            }
            else if (currentOwner == player.PlayerId)
            {
                QubitOwners.Set(qubitId, 0);
            }
        }

        private void SetAuthoritativePose(int qubitId, Vector3 localPosition, Quaternion localRotation)
        {
            LocalPositions.Set(qubitId, localPosition);
            LocalRotations.Set(qubitId, localRotation);
        }

        private void ApplyAuthoritativePoses()
        {
            var qubits = FindQubitsById();
            if (qubits == null)
            {
                return;
            }
            for (int i = 0; i < QubitCount; i++)
            {
                if (qubits[i].IsLocallyGrabbed && QubitOwners[i] == Runner.LocalPlayer.PlayerId)
                {
                    continue;
                }
                ToWorldPose(LocalPositions[i], LocalRotations[i], out Vector3 position, out Quaternion rotation);
                qubits[i].transform.SetPositionAndRotation(position, rotation);
            }
        }

        private void ApplyReplicatedPoses()
        {
            var qubits = FindQubitsById();
            if (qubits == null)
            {
                return;
            }
            for (int i = 0; i < QubitCount; i++)
            {
                if (qubits[i].IsLocallyGrabbed && IsLocalGrabApproved(i))
                {
                    continue;
                }
                ToWorldPose(LocalPositions[i], LocalRotations[i], out Vector3 position, out Quaternion rotation);
                qubits[i].transform.SetPositionAndRotation(position, rotation);
            }
        }

        private void PublishDensityMatrix(bool force = false)
        {
            ComplexMatrix state = QubitManager.GetDensityMatrix();
            if (state == null || state.Rows != 8 || state.Columns != 8)
            {
                return;
            }

            if (!force && Time.unscaledTime < nextSnapshotPublishTime)
            {
                return;
            }
            nextSnapshotPublishTime = Time.unscaledTime + SnapshotSendInterval;

            int index = 0;
            uint hash = 2166136261;
            for (int row = 0; row < 8; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    Complex value = state[row, column];
                    float real = (float)value.Real;
                    float imaginary = (float)value.Imaginary;
                    DensityData.Set(index++, real);
                    DensityData.Set(index++, imaginary);
                    hash = HashFloat(hash, real);
                    hash = HashFloat(hash, imaginary);
                }
            }

            QuantumStateVersion = checked((int)stateVersion());
            QuantumStateHash = hash;
            SnapshotReady = true;

            long stateVersion()
            {
                EntanglementSnapshot snapshot = QubitManager.GetEntanglementSnapshot();
                return snapshot != null ? snapshot.StateVersion : 0L;
            }
        }

        private void ApplyRemoteDensityMatrix()
        {
            var state = new ComplexMatrix(8, 8);
            int index = 0;
            for (int row = 0; row < 8; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    state[row, column] = new Complex(DensityData[index++], DensityData[index++]);
                }
            }

            try
            {
                QubitManager.ApplyAuthoritativeSnapshot(state, QuantumStateVersion);
                lastAppliedVersion = QuantumStateVersion;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[QuantumSessionState] Rejected host quantum snapshot: {exception.Message}", this);
            }
        }

        private Qubit[] FindQubitsById()
        {
            if (qubitsById != null)
            {
                bool valid = true;
                for (int i = 0; i < QubitCount; i++)
                {
                    if (qubitsById[i] == null || qubitsById[i].GetIndex() != i)
                    {
                        valid = false;
                        break;
                    }
                }
                if (valid)
                {
                    return qubitsById;
                }
            }

            var result = new Qubit[QubitCount];
            foreach (Qubit qubit in FindObjectsOfType<Qubit>())
            {
                int id = qubit.GetIndex();
                if (IsValidQubitId(id))
                {
                    result[id] = qubit;
                }
            }
            for (int i = 0; i < QubitCount; i++)
            {
                if (result[i] == null)
                {
                    return null;
                }
            }
            qubitsById = result;
            return qubitsById;
        }

        private void ToSharedLocalPose(Vector3 worldPosition, Quaternion worldRotation, out Vector3 localPosition, out Quaternion localRotation)
        {
            Transform root = sharedSpaceAligner != null ? sharedSpaceAligner.SharedSpaceRoot : null;
            localPosition = root != null ? root.InverseTransformPoint(worldPosition) : worldPosition;
            localRotation = root != null ? Quaternion.Inverse(root.rotation) * worldRotation : worldRotation;
        }

        private void ToWorldPose(Vector3 localPosition, Quaternion localRotation, out Vector3 worldPosition, out Quaternion worldRotation)
        {
            Transform root = sharedSpaceAligner != null ? sharedSpaceAligner.SharedSpaceRoot : null;
            worldPosition = root != null ? root.TransformPoint(localPosition) : localPosition;
            worldRotation = root != null ? root.rotation * localRotation : localRotation;
        }

        private static bool IsValidQubitId(int qubitId) => qubitId >= 0 && qubitId < QubitCount;

        private static uint HashFloat(uint hash, float value)
        {
            return (hash ^ unchecked((uint)value.GetHashCode())) * 16777619;
        }
    }
}
#else
namespace ArtsOfEntanglement.Colocation
{
    /// <summary>Single-player fallback when Fusion is not compiled into the project.</summary>
    public sealed class QuantumSessionState : MonoBehaviour
    {
        public static bool IsNetworkSessionActive => false;
        public static bool RequestGate(int qubitId, QuantumGateOperation operation) => false;
        public static bool RequestMeasurement(int qubitId) => false;
        public static void BeginLocalGrab(int qubitId) { }
        public static void EndLocalGrab(int qubitId) { }
        public static bool IsLocalGrabApproved(int qubitId) => false;
        public static void SubmitLocalPose(int qubitId, Vector3 worldPosition, Quaternion worldRotation) { }
    }
}
#endif

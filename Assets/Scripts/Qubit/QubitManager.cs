using System;
using System.Collections.Generic;
using ArtsOfEntanglement.Colocation;
using TMPro;
using UnityEngine;

/// <summary>
/// Scene adapter for QuantumStateEngine. Qubit IDs, rather than discovery order,
/// define the computational basis ordering.
/// </summary>
public class QubitManager : MonoBehaviour
{
    private static QubitManager instance;
    private static QuantumStateEngine engine;
    private static EntanglementSnapshot currentSnapshot;
    private static readonly Dictionary<int, Qubit> qubitLookup = new Dictionary<int, Qubit>();
    private static double[,] pairCouplings;
    private readonly List<Qubit> allQubits = new List<Qubit>();

    [SerializeField, Min(0f)] private float simulationTimeScale = 1f;
    [SerializeField] private bool validateEveryFixedStep = true;
    [SerializeField] private bool applyInitialPauliX = true;

    public static int numQubits;
    public static float THRESHOLD_DISTANCE = 2f;
    public static double entropy;
    public static float[] J;
    public TMP_Text textMeshPro;
    public static float volume = 0.8f;

    private void Awake()
    {
        instance = this;
        InitializeFromScene();
    }

    private void Start()
    {
        if (applyInitialPauliX && allQubits.Count > 0)
        {
            Invoke(nameof(ApplyInitialGate), 0.5f);
        }
    }

    private void Update()
    {
        if (currentSnapshot != null && currentSnapshot.Nodes.Count > 0)
        {
            entropy = currentSnapshot.GetNode(0).Renyi2Entropy;
        }
    }

    private void FixedUpdate()
    {
        // Fusion's state authority owns simulation whenever a shared quantum
        // session exists. Clients only render the replicated density matrix.
        if (QuantumSessionState.IsNetworkSessionActive)
        {
            return;
        }

        AdvanceSimulation(Time.fixedDeltaTime);
    }

    /// <summary>
    /// Advances the global state exactly once. In a Fusion session this is
    /// called by QuantumSessionState.FixedUpdateNetwork on state authority.
    /// </summary>
    public static void AdvanceSimulation(double deltaTime)
    {
        if (engine == null)
        {
            return;
        }

        var manager = instance;
        if (manager == null)
        {
            return;
        }

        List<QubitPairCoupling> couplings = CalculateProximity(manager.allQubits, THRESHOLD_DISTANCE);
        engine.StepHeisenberg(couplings, deltaTime * manager.simulationTimeScale);
        if (manager.validateEveryFixedStep)
        {
            DensityMatrixValidationResult validation = engine.ValidateState(1e-7);
            if (!validation.IsValid)
            {
                Debug.LogError($"[QuantumStateEngine] Invalid state after fixed step: {validation.Message}", manager);
                manager.enabled = false;
                return;
            }
        }

        RefreshSnapshot();
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(instance, this))
        {
            instance = null;
        }
        if (engine != null)
        {
            engine = null;
            currentSnapshot = null;
            numQubits = 0;
            J = null;
            pairCouplings = null;
            entropy = 0.0;
            qubitLookup.Clear();
        }
    }

    private void InitializeFromScene()
    {
        allQubits.Clear();
        qubitLookup.Clear();

        GameObject[] objects = GameObject.FindGameObjectsWithTag("Qubit");
        foreach (GameObject item in objects)
        {
            if (!item.TryGetComponent(out Qubit qubit))
            {
                continue;
            }
            if (qubitLookup.ContainsKey(qubit.GetIndex()))
            {
                throw new InvalidOperationException(
                    $"Duplicate Qubit ID {qubit.GetIndex()} on '{qubitLookup[qubit.GetIndex()].name}' and '{qubit.name}'.");
            }
            qubitLookup.Add(qubit.GetIndex(), qubit);
            allQubits.Add(qubit);
        }

        allQubits.Sort((left, right) => left.GetIndex().CompareTo(right.GetIndex()));
        for (int expectedId = 0; expectedId < allQubits.Count; expectedId++)
        {
            if (allQubits[expectedId].GetIndex() != expectedId)
            {
                throw new InvalidOperationException(
                    $"Qubit IDs must be contiguous from 0. Expected {expectedId}, found {allQubits[expectedId].GetIndex()} on '{allQubits[expectedId].name}'.");
            }
        }
        if (allQubits.Count == 0)
        {
            throw new InvalidOperationException("No active GameObjects tagged 'Qubit' were found.");
        }

        numQubits = allQubits.Count;
        J = new float[numQubits];
        pairCouplings = new double[numQubits, numQubits];
        engine = new QuantumStateEngine(numQubits);
        RefreshSnapshot(true);
        Debug.Log($"[QuantumStateEngine] Initialized {numQubits} qubits ({engine.Dimension}x{engine.Dimension} density matrix).", this);
    }

    private void ApplyInitialGate()
    {
        if (allQubits.Count > 0)
        {
            ApplyPauliX(allQubits[0]);
        }
    }

    public static ComplexMatrix GetDensityMatrix() => engine?.DensityMatrix;
    public static EntanglementSnapshot GetEntanglementSnapshot() => currentSnapshot;
    public static int GetQubits() => engine?.QubitCount ?? 0;
    public static bool IsInitialized => engine != null;

    // Kept for old display/debug scripts. Initialization is now atomic.
    public static int GetInitQubits() => GetQubits();

    internal static void RegisterQubitInstance(Qubit qubit)
    {
        if (qubit != null)
        {
            qubitLookup[qubit.GetIndex()] = qubit;
        }
    }

    internal static void UnregisterQubitInstance(Qubit qubit)
    {
        if (qubit != null && qubitLookup.TryGetValue(qubit.GetIndex(), out Qubit registered) &&
            ReferenceEquals(registered, qubit))
        {
            qubitLookup.Remove(qubit.GetIndex());
        }
    }

    public static void ApplyPauliX(Qubit qubit) => RequestGate(qubit, QuantumGateOperation.PauliX);
    public static void ApplyPauliZ(Qubit qubit) => RequestGate(qubit, QuantumGateOperation.PauliZ);
    public static void ApplyHadamard(Qubit qubit) => RequestGate(qubit, QuantumGateOperation.Hadamard);
    public static void ApplyPhaseGate(Qubit qubit) => RequestGate(qubit, QuantumGateOperation.PhaseS);

    private static void RequestGate(Qubit qubit, QuantumGateOperation operation)
    {
        if (qubit == null)
        {
            throw new ArgumentNullException(nameof(qubit));
        }

        if (QuantumSessionState.RequestGate(qubit.GetIndex(), operation))
        {
            return;
        }

        ApplyGateLocally(qubit.GetIndex(), operation);
    }

    /// <summary>Called only by the local single-player path or Fusion state authority.</summary>
    public static void ApplyGateLocally(int qubitId, QuantumGateOperation operation)
    {
        EnsureInitialized();
        switch (operation)
        {
            case QuantumGateOperation.Hadamard:
                engine.ApplySingleQubitGate(qubitId, Gates.Hadamard());
                break;
            case QuantumGateOperation.PauliX:
                engine.ApplySingleQubitGate(qubitId, Gates.PauliX());
                break;
            case QuantumGateOperation.PauliZ:
                engine.ApplySingleQubitGate(qubitId, Gates.PauliZ());
                break;
            case QuantumGateOperation.PhaseS:
                engine.ApplySingleQubitGate(qubitId, Gates.PhaseS());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
        RefreshSnapshot();
    }

    public static void Measure(Qubit qubit)
    {
        if (qubit == null)
        {
            throw new ArgumentNullException(nameof(qubit));
        }
        Measure(qubit.GetIndex());
    }

    public static void Measure(int qubitId)
    {
        EnsureInitialized();
        if (QuantumSessionState.RequestMeasurement(qubitId))
        {
            return;
        }

        MeasurementResult result = MeasureLocally(qubitId, UnityEngine.Random.value);
        Debug.Log($"Measured Q{result.QubitId}: {result.Outcome} (p={result.Probability:F4}).");
    }

    /// <summary>Called only by the local single-player path or Fusion state authority.</summary>
    public static MeasurementResult MeasureLocally(int qubitId, double randomSample)
    {
        EnsureInitialized();
        MeasurementResult result = engine.MeasureZ(qubitId, randomSample);
        RefreshSnapshot();
        return result;
    }

    /// <summary>Installs the host's density matrix without running local evolution.</summary>
    public static void ApplyAuthoritativeSnapshot(ComplexMatrix state, long authoritativeVersion)
    {
        EnsureInitialized();
        engine.SetDensityMatrix(state);
        currentSnapshot = EntanglementMetrics.Build(
            engine.DensityMatrix, engine.QubitCount, authoritativeVersion);
    }

    public static ComplexMatrix PartialTrace(int index)
    {
        EnsureInitialized();
        return engine.PartialTrace(index);
    }

    public static ComplexMatrix PartialTrace(params int[] keepQubitIds)
    {
        EnsureInitialized();
        return engine.PartialTrace(keepQubitIds);
    }

    public static double Entropy(int index)
    {
        EnsureInitialized();
        return engine.ComputeRenyi2Entropy(index);
    }

    /// <summary>Legacy two-qubit debug API. Runtime proximity uses the composite Hamiltonian path.</summary>
    public static void ApplySpinExchange(float coupling, float time)
    {
        EnsureInitialized();
        if (engine.QubitCount != 2)
        {
            throw new InvalidOperationException(
                "ApplySpinExchange(J,time) is a two-qubit compatibility API. Use pair couplings for three or more qubits.");
        }
        engine.StepHeisenberg(new[] { new QubitPairCoupling(0, 1, coupling) }, time);
        RefreshSnapshot();
    }

    public static DensityMatrixValidationResult ValidateDensityMatrix(double tolerance = 1e-9)
    {
        EnsureInitialized();
        return engine.ValidateState(tolerance);
    }

    public static double GetPairCoupling(int firstQubitId, int secondQubitId)
    {
        if (pairCouplings == null || firstQubitId < 0 || secondQubitId < 0 ||
            firstQubitId >= pairCouplings.GetLength(0) || secondQubitId >= pairCouplings.GetLength(1))
        {
            return 0.0;
        }
        return pairCouplings[firstQubitId, secondQubitId];
    }

    private static List<QubitPairCoupling> CalculateProximity(List<Qubit> qubits, float threshold)
    {
        var couplings = new List<QubitPairCoupling>();
        if (J == null || J.Length != qubits.Count)
        {
            J = new float[qubits.Count];
        }
        Array.Clear(J, 0, J.Length);
        if (pairCouplings == null || pairCouplings.GetLength(0) != qubits.Count)
        {
            pairCouplings = new double[qubits.Count, qubits.Count];
        }
        else
        {
            Array.Clear(pairCouplings, 0, pairCouplings.Length);
        }

        for (int first = 0; first < qubits.Count; first++)
        {
            for (int second = first + 1; second < qubits.Count; second++)
            {
                Qubit qubitA = qubits[first];
                Qubit qubitB = qubits[second];
                float distance = Vector3.Distance(qubitA.transform.position, qubitB.transform.position);
                if (distance > threshold)
                {
                    continue;
                }

                float strength = 0.5f * (1f + (float)Math.Tanh(threshold / 2f) - distance);
                J[qubitA.GetIndex()] = Mathf.Max(J[qubitA.GetIndex()], Mathf.Abs(strength));
                J[qubitB.GetIndex()] = Mathf.Max(J[qubitB.GetIndex()], Mathf.Abs(strength));
                pairCouplings[qubitA.GetIndex(), qubitB.GetIndex()] = strength;
                pairCouplings[qubitB.GetIndex(), qubitA.GetIndex()] = strength;
                couplings.Add(new QubitPairCoupling(
                    qubitA.GetIndex(), qubitB.GetIndex(), strength));
            }
        }
        return couplings;
    }

    private static void EnsureInitialized()
    {
        if (engine == null)
        {
            throw new InvalidOperationException("QuantumStateEngine has not been initialized by QubitManager.Awake.");
        }
    }

    private static void RefreshSnapshot(bool force = false)
    {
        if (engine == null)
        {
            currentSnapshot = null;
            return;
        }
        if (!force && currentSnapshot != null && currentSnapshot.StateVersion == engine.StateVersion)
        {
            return;
        }
        currentSnapshot = EntanglementMetrics.Build(
            engine.DensityMatrix, engine.QubitCount, engine.StateVersion);
    }
}

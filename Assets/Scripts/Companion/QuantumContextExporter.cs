using System;
using System.Collections.Generic;
using ArtsOfEntanglement.Colocation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Qixr.Companion
{
    public sealed class QuantumContextExporter : MonoBehaviour
    {
        public string RoomId { get; set; }
        public int AuthorityEpoch { get; set; }
        public int EventSequence { get; set; }
        public static bool AllowStandalonePublishing { get; set; }
        public static bool JoinOnlyParticipant { get; set; }
        private static bool networkObserved;
        public static bool NetworkStateRequired
        {
            get
            {
#if FUSION_PRESENT || FUSION_WEAVER
                if (SessionManager.Instance != null && SessionManager.Instance.Runner != null) networkObserved=true;
#endif
                return JoinOnlyParticipant || networkObserved;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPolicy() { networkObserved=false; AllowStandalonePublishing=false; JoinOnlyParticipant=false; }
        public static bool IsAuthority
        {
            get
            {
#if FUSION_PRESENT || FUSION_WEAVER
                if (SessionManager.Instance != null && SessionManager.Instance.Runner != null) networkObserved=true;
                if (QuantumSessionState.IsNetworkSessionActive)
                    return !JoinOnlyParticipant && QuantumSessionState.Instance != null && QuantumSessionState.Instance.HasStateAuthority;
                return !JoinOnlyParticipant && AllowStandalonePublishing && !networkObserved;
#else
                return !JoinOnlyParticipant && AllowStandalonePublishing;
#endif
            }
        }

        // Capture is invoked on the main thread. No Unity object escapes into a worker.
        public QuantumSnapshotDto Capture(ComplexMatrix state = null, long version = -1, int eventSequence = -1)
        {
            if (!IsAuthority || AuthorityEpoch < 1 || !QubitManager.IsInitialized) return null;
            ComplexMatrix copy = (state ?? QubitManager.GetDensityMatrix()).Clone();
            int n = QubitManager.GetQubits();
            var density = new double[copy.Rows * copy.Columns * 2];
            int index = 0;
            for (int r = 0; r < copy.Rows; r++)
                for (int c = 0; c < copy.Columns; c++)
                {
                    density[index++] = copy[r,c].Real;
                    density[index++] = copy[r,c].Imaginary;
                }
            var couplings = new List<CouplingDto>();
            for (int a = 0; a < n; a++)
                for (int b = a+1; b < n; b++)
                    couplings.Add(new CouplingDto { first = a, second = b, strength = QubitManager.GetPairCoupling(a,b) });
            // Read the actual serialized thresholds from the active components.
            var p = new PresentationDto { scene = SceneManager.GetActiveScene().name };
            Entanglement visual = FindFirstObjectByType<Entanglement>();
            QuantumArpeggioComposer music = FindFirstObjectByType<QuantumArpeggioComposer>();
            if (visual != null) { p.ribbonOn = visual.RibbonActivationThreshold; p.ribbonOff = visual.RibbonReleaseThreshold; }
            if (music != null) p.arpeggioOn = music.EntryThreshold;
            return new QuantumSnapshotDto {
                roomId = RoomId, authorityEpoch = AuthorityEpoch, snapshotId = Guid.NewGuid().ToString("N"),
                stateVersion = version >= 0 ? version : QubitManager.GetEntanglementSnapshot().StateVersion,
                eventSequence = eventSequence >= 0 ? eventSequence : EventSequence,
                simulationTime = Time.timeAsDouble, capturedAtUtc = DateTime.UtcNow.ToString("O"),
                qubitCount = n, density = density, couplings = couplings.ToArray(), presentation = p
            };
        }
    }
}

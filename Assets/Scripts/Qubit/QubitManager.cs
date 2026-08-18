using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Scene adapter for QuantumStateEngine. Qubit IDs, rather than discovery order,
/// define the computational basis ordering.
/// </summary>
public class QubitManager : MonoBehaviour
{
    private static QuantumStateEngine engine;
    private static readonly Dictionary<int, Qubit> qubitLookup = new Dictionary<int, Qubit>();
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
        if (engine != null && engine.QubitCount > 0)
        {
            entropy = engine.ComputeRenyi2Entropy(0);
        }
    }

    private void FixedUpdate()
    {
        if (engine == null)
        {
            return;
        }

        List<QubitPairCoupling> couplings = CalculateProximity(allQubits, THRESHOLD_DISTANCE);
        engine.StepHeisenberg(couplings, Time.fixedDeltaTime * simulationTimeScale);

        if (validateEveryFixedStep)
        {
            DensityMatrixValidationResult validation = engine.ValidateState(1e-7);
            if (!validation.IsValid)
            {
                Debug.LogError($"[QuantumStateEngine] Invalid state after fixed step: {validation.Message}", this);
                enabled = false;
            }
        }
    }

    private void OnDestroy()
    {
        if (engine != null)
        {
            engine = null;
            numQubits = 0;
            J = null;
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
        engine = new QuantumStateEngine(numQubits);
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
    public static int GetQubits() => engine?.QubitCount ?? 0;

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

    public static void ApplyPauliX(Qubit qubit) => ApplySingleGate(qubit, Gates.PauliX());
    public static void ApplyPauliZ(Qubit qubit) => ApplySingleGate(qubit, Gates.PauliZ());
    public static void ApplyHadamard(Qubit qubit) => ApplySingleGate(qubit, Gates.Hadamard());
    public static void ApplyPhaseGate(Qubit qubit) => ApplySingleGate(qubit, Gates.PhaseS());

    private static void ApplySingleGate(Qubit qubit, ComplexMatrix gate)
    {
        if (qubit == null)
        {
            throw new ArgumentNullException(nameof(qubit));
        }
        EnsureInitialized();
        engine.ApplySingleQubitGate(qubit.GetIndex(), gate);
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
        MeasurementResult result = engine.MeasureZ(qubitId, UnityEngine.Random.value);
        Debug.Log($"Measured Q{result.QubitId}: {result.Outcome} (p={result.Probability:F4}).");
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
    }

    public static DensityMatrixValidationResult ValidateDensityMatrix(double tolerance = 1e-9)
    {
        EnsureInitialized();
        return engine.ValidateState(tolerance);
    }

    private static List<QubitPairCoupling> CalculateProximity(List<Qubit> qubits, float threshold)
    {
        var couplings = new List<QubitPairCoupling>();
        if (J == null || J.Length != qubits.Count)
        {
            J = new float[qubits.Count];
        }
        Array.Clear(J, 0, J.Length);

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
}

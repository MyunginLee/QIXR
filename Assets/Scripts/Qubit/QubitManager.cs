using Complex = System.Numerics.Complex;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static Gates;

public class QubitManager : MonoBehaviour
{
    private static ComplexMatrix densityMatrix;
    public static int numQubits = 0;
    private static int initQubits = 0;
    private static readonly HashSet<QubitPair> entangledPairs = new HashSet<QubitPair>();
    private static readonly Dictionary<int, Qubit> qubitLookup = new Dictionary<int, Qubit>();

    private readonly List<Qubit> allQubits = new List<Qubit>();
    private float time = 1f;
    public static float THRESHOLD_DISTANCE = 2f;
    public static double entropy;
    public static float[] J;
    public TMP_Text textMeshPro;
    public static float volume = 0.8f;

    private void Start()
    {
        GameObject[] qubits = GameObject.FindGameObjectsWithTag("Qubit");
        J = new float[qubits.Length];

        foreach (GameObject qubit in qubits)
        {
            if (qubit.TryGetComponent(out Qubit qubitComponent))
            {
                allQubits.Add(qubitComponent);
            }
        }

        Invoke(nameof(ApplyGate), 0.5f);
        InvokeRepeating(nameof(UpdateSpinExchange), 0.1f, 0.1f);
    }

    private void Update()
    {
        entropy = Entropy(0);
    }

    private void UpdateSpinExchange()
    {
        J = CalculateProximity(allQubits, time, THRESHOLD_DISTANCE);
    }

    private void ApplyGate()
    {
        if (allQubits.Count > 0)
        {
            ApplyPauliX(allQubits[0]);
        }
    }

    public static void UpdateDensityMatrix()
    {
        if (densityMatrix == null)
        {
            densityMatrix = ComplexMatrix.FromArray(new Complex[,]
            {
                { 1, 0 },
                { 0, 0 }
            });
        }
        else
        {
            densityMatrix = densityMatrix.KroneckerProduct(UpMatrix());
        }

        numQubits++;
    }

    public static ComplexMatrix GetDensityMatrix()
    {
        return densityMatrix;
    }

    public static int GetQubits()
    {
        return numQubits;
    }

    public static int GetInitQubits()
    {
        return initQubits;
    }

    public static void IncrementInitQubits()
    {
        initQubits += 1;
    }

    internal static void RegisterQubitInstance(Qubit qubit)
    {
        if (qubit == null)
        {
            return;
        }

        qubitLookup[qubit.GetIndex()] = qubit;
    }

    internal static void UnregisterQubitInstance(Qubit qubit)
    {
        if (qubit == null)
        {
            return;
        }

        qubitLookup.Remove(qubit.GetIndex());
        RemoveEntanglementReferences(qubit);
    }

    public static void ApplyPauliX(Qubit qubit)
    {
        ApplyGateAcrossEntanglement(qubit, q => q.GetPauliX());
    }

    public static void ApplyPauliZ(Qubit qubit)
    {
        ApplyGateAcrossEntanglement(qubit, q => q.GetPauliZ());
    }

    public static void ApplyHadamard(Qubit qubit)
    {
        ApplyGateAcrossEntanglement(qubit, q => q.GetHadamard());
    }

    public static void ApplyPhaseGate(Qubit qubit)
    {
        ApplyGateAcrossEntanglement(qubit, q => q.GetPhaseS(), q => q.GetPhaseSDagger());
    }

    public static void Measure(Qubit qubit)
    {
        if (qubit == null)
        {
            throw new ArgumentNullException(nameof(qubit));
        }

        PerformMeasurement(qubit.GetIndex(), qubit);
    }

    public static void Measure(int index)
    {
        qubitLookup.TryGetValue(index, out Qubit qubit);
        PerformMeasurement(index, qubit);
    }

    private static void PerformMeasurement(int index, Qubit qubit)
    {
        List<int> measurementOrder = BuildMeasurementOrder(index, qubit);

        foreach (int targetIndex in measurementOrder)
        {
            CollapseSingleQubit(targetIndex);
        }

        ResetEntanglementGraph();
    }

    private static List<int> BuildMeasurementOrder(int index, Qubit qubit)
    {
        var order = new List<int>();

        if (qubit != null)
        {
            foreach (Qubit target in ResolveGateTargets(qubit))
            {
                int targetIndex = target.GetIndex();
                if (!order.Contains(targetIndex))
                {
                    order.Add(targetIndex);
                }
            }
        }

        if (!order.Contains(index) && index >= 0)
        {
            order.Insert(0, index);
        }

        if (order.Count == 0 && index >= 0)
        {
            order.Add(index);
        }

        return order;
    }

    private static void CollapseSingleQubit(int index)
    {
        if (densityMatrix == null)
        {
            throw new InvalidOperationException("Density matrix is not initialised.");
        }
        if (numQubits == 0)
        {
            throw new InvalidOperationException("No qubits registered in the system.");
        }
        if (index < 0 || index >= numQubits)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Measurement index is out of range.");
        }

        int totalQubits = numQubits;
        int dimension = densityMatrix.Rows;
        int targetBit = totalQubits - 1 - index;

        double prob0 = 0.0;
        double prob1 = 0.0;
        for (int basis = 0; basis < dimension; basis++)
        {
            int bit = (basis >> targetBit) & 1;
            double value = densityMatrix[basis, basis].Real;
            if (bit == 0)
            {
                prob0 += value;
            }
            else
            {
                prob1 += value;
            }
        }

        prob0 = Math.Max(0.0, prob0);
        prob1 = Math.Max(0.0, prob1);
        double total = prob0 + prob1;
        if (total <= double.Epsilon)
        {
            prob0 = 1.0;
            prob1 = 0.0;
            total = 1.0;
        }

        prob0 /= total;
        prob1 = 1.0 - prob0;

        double randomValue = UnityEngine.Random.value;
        int state = randomValue <= prob0 ? 0 : 1;
        double probability = state == 0 ? prob0 : prob1;
        probability = Math.Max(probability, 1e-8);

        var collapsed = new ComplexMatrix(dimension, dimension);
        for (int row = 0; row < dimension; row++)
        {
            int rowState = (row >> targetBit) & 1;
            if (rowState != state)
            {
                continue;
            }

            for (int col = 0; col < dimension; col++)
            {
                int colState = (col >> targetBit) & 1;
                if (colState != state)
                {
                    continue;
                }

                collapsed[row, col] = densityMatrix[row, col];
            }
        }

        densityMatrix = collapsed / probability;
    }

    public static ComplexMatrix PartialTrace(int index)
    {
        if (densityMatrix == null)
        {
            throw new InvalidOperationException("Density matrix is not initialised.");
        }
        if (numQubits == 0)
        {
            throw new InvalidOperationException("No qubits registered in the system.");
        }

        int dimension = densityMatrix.Rows;
        int totalQubits = numQubits;
        int targetBit = totalQubits - 1 - index;
        var reduced = new ComplexMatrix(2, 2);

        for (int row = 0; row < dimension; row++)
        {
            int rowState = (row >> targetBit) & 1;
            int rowRest = RemoveBit(row, targetBit);

            for (int col = 0; col < dimension; col++)
            {
                int colState = (col >> targetBit) & 1;
                if (RemoveBit(col, targetBit) == rowRest)
                {
                    reduced[rowState, colState] = reduced[rowState, colState] + densityMatrix[row, col];
                }
            }
        }

        double trace = reduced.Trace().Real;
        if (trace > double.Epsilon)
        {
            reduced = reduced / trace;
        }

        return reduced;
    }

    public static double Entropy(int index)
    {
        ComplexMatrix reduced = PartialTrace(index);
        ComplexMatrix squared = reduced * reduced;
        double purity = squared.Trace().Real;
        purity = Math.Min(1.0, Math.Max(purity, 1e-8));
        return -Math.Log(purity);
    }

    public static void ApplySpinExchange(float J, float time)
    {
        ComplexMatrix U = SpinExchange(J, time);
        densityMatrix = U * densityMatrix * U.ConjugateTranspose();
    }

    private float[] CalculateProximity(List<Qubit> qList, float currentTime, float threshold)
    {
        float[] couplings = new float[qList.Count];
        for (int i = 0; i < qList.Count; i++)
        {
            for (int j = i + 1; j < qList.Count; j++)
            {
                Qubit qubitA = qList[i];
                Qubit qubitB = qList[j];

                float distance = Vector3.Distance(qubitA.transform.position, qubitB.transform.position);
                if (distance <= threshold)
                {
                    float Jmax = 1f;
                    float value = Jmax / 2f * (1f + (float)Math.Tanh(threshold / 2f) - distance);
                    couplings[i] = value;
                    couplings[j] = value;
                    ApplySpinExchange(value, currentTime);
                    RegisterEntanglementPair(qubitA, qubitB);
                }
            }
        }

        return couplings;
    }

    private static void ApplyGateAcrossEntanglement(
        Qubit source,
        Func<Qubit, ComplexMatrix> leftFactory,
        Func<Qubit, ComplexMatrix> rightFactory = null)
    {
        if (source == null || densityMatrix == null || leftFactory == null)
        {
            return;
        }

        foreach (Qubit target in ResolveGateTargets(source))
        {
            if (target == null)
            {
                continue;
            }

            ComplexMatrix left = leftFactory(target);
            ComplexMatrix right = rightFactory?.Invoke(target) ?? left;
            densityMatrix = left * densityMatrix * right;
        }
    }

    private static List<Qubit> ResolveGateTargets(Qubit source)
    {
        var targets = new List<Qubit>();
        if (source == null)
        {
            return targets;
        }

        var visited = new HashSet<Qubit>();
        var queue = new Queue<Qubit>();
        visited.Add(source);
        queue.Enqueue(source);

        while (queue.Count > 0)
        {
            Qubit current = queue.Dequeue();
            targets.Add(current);

            foreach (Qubit neighbor in GetEntangledNeighbors(current))
            {
                if (neighbor != null && visited.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        return targets;
    }

    private static IEnumerable<Qubit> GetEntangledNeighbors(Qubit qubit)
    {
        foreach (QubitPair pair in entangledPairs)
        {
            if (pair.Contains(qubit))
            {
                Qubit other = pair.Other(qubit);
                if (other != null)
                {
                    yield return other;
                }
            }
        }
    }

    private static void RegisterEntanglementPair(Qubit first, Qubit second)
    {
        if (first == null || second == null || ReferenceEquals(first, second))
        {
            return;
        }

        entangledPairs.Add(QubitPair.Create(first, second));
    }

    private static void RemoveEntanglementReferences(Qubit qubit)
    {
        if (qubit == null || entangledPairs.Count == 0)
        {
            return;
        }

        var toRemove = new List<QubitPair>();
        foreach (QubitPair pair in entangledPairs)
        {
            if (pair.Contains(qubit))
            {
                toRemove.Add(pair);
            }
        }

        foreach (QubitPair pair in toRemove)
        {
            entangledPairs.Remove(pair);
        }
    }

    private static void ResetEntanglementGraph()
    {
        entangledPairs.Clear();
    }

    private static int RemoveBit(int value, int bitPosition)
    {
        int lowerMask = (1 << bitPosition) - 1;
        int lower = value & lowerMask;
        int upper = value >> (bitPosition + 1);
        return (upper << bitPosition) | lower;
    }

    private readonly struct QubitPair : IEquatable<QubitPair>
    {
        public QubitPair(Qubit first, Qubit second)
        {
            First = first;
            Second = second;
        }

        public Qubit First { get; }
        public Qubit Second { get; }

        public static QubitPair Create(Qubit first, Qubit second)
        {
            int firstId = first.GetInstanceID();
            int secondId = second.GetInstanceID();
            return firstId <= secondId ? new QubitPair(first, second) : new QubitPair(second, first);
        }

        public bool Contains(Qubit qubit)
        {
            return ReferenceEquals(qubit, First) || ReferenceEquals(qubit, Second);
        }

        public Qubit Other(Qubit qubit)
        {
            if (ReferenceEquals(qubit, First))
            {
                return Second;
            }

            if (ReferenceEquals(qubit, Second))
            {
                return First;
            }

            return null;
        }

        public bool Equals(QubitPair other)
        {
            return ReferenceEquals(First, other.First) && ReferenceEquals(Second, other.Second);
        }

        public override bool Equals(object obj)
        {
            return obj is QubitPair other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashFirst = First != null ? First.GetInstanceID() : 0;
                int hashSecond = Second != null ? Second.GetInstanceID() : 0;
                return (hashFirst * 397) ^ hashSecond;
            }
        }
    }
}



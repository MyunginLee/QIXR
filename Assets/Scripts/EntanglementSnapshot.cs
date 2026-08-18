using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Complex = System.Numerics.Complex;
using UnityEngine;

/// <summary>
/// Immutable quantitative view of one global density-matrix state. Renderers and
/// audio read this object so they cannot disagree about what "entangled" means.
/// </summary>
public sealed class EntanglementSnapshot
{
    private readonly QubitMetric[] nodes;
    private readonly PairEntanglementMetric[] pairs;
    private readonly ReadOnlyCollection<QubitMetric> readOnlyNodes;
    private readonly ReadOnlyCollection<PairEntanglementMetric> readOnlyPairs;

    internal EntanglementSnapshot(
        long stateVersion,
        QubitMetric[] nodes,
        PairEntanglementMetric[] pairs,
        bool hasThreePartyCorrelation,
        double threePartyCorrelationStrength,
        double globalPurity,
        DensityMatrixValidationResult validation)
    {
        StateVersion = stateVersion;
        this.nodes = nodes;
        this.pairs = pairs;
        readOnlyNodes = Array.AsReadOnly(nodes);
        readOnlyPairs = Array.AsReadOnly(pairs);
        HasThreePartyCorrelation = hasThreePartyCorrelation;
        ThreePartyCorrelationStrength = threePartyCorrelationStrength;
        GlobalPurity = globalPurity;
        Validation = validation;
    }

    public long StateVersion { get; }
    public IReadOnlyList<QubitMetric> Nodes => readOnlyNodes;
    public IReadOnlyList<PairEntanglementMetric> Pairs => readOnlyPairs;
    public bool HasThreePartyCorrelation { get; }
    public double ThreePartyCorrelationStrength { get; }
    public double GlobalPurity { get; }
    public bool IsGloballyPure => GlobalPurity >= 1.0 - 1e-7;
    public DensityMatrixValidationResult Validation { get; }

    public QubitMetric GetNode(int qubitId)
    {
        if (qubitId < 0 || qubitId >= nodes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(qubitId));
        }
        return nodes[qubitId];
    }

    public PairEntanglementMetric GetPair(int firstQubitId, int secondQubitId)
    {
        int low = Math.Min(firstQubitId, secondQubitId);
        int high = Math.Max(firstQubitId, secondQubitId);
        for (int i = 0; i < pairs.Length; i++)
        {
            if (pairs[i].FirstQubitId == low && pairs[i].SecondQubitId == high)
            {
                return pairs[i];
            }
        }
        throw new ArgumentException($"No pair metric exists for Q{firstQubitId}-Q{secondQubitId}.");
    }
}

public sealed class QubitMetric
{
    private readonly ComplexMatrix reducedState;

    internal QubitMetric(
        int qubitId, ComplexMatrix reducedState, Vector3 blochVector,
        double purity, double renyi2Entropy, double vonNeumannEntropyBits)
    {
        QubitId = qubitId;
        this.reducedState = reducedState;
        BlochVector = blochVector;
        BlochRadius = blochVector.magnitude;
        Purity = purity;
        Renyi2Entropy = renyi2Entropy;
        VonNeumannEntropyBits = vonNeumannEntropyBits;
    }

    public int QubitId { get; }
    public Vector3 BlochVector { get; }
    public float BlochRadius { get; }
    public double Purity { get; }
    public double Renyi2Entropy { get; }
    public double VonNeumannEntropyBits { get; }
    public ComplexMatrix GetReducedStateCopy() => reducedState.Clone();
}

public sealed class PairEntanglementMetric
{
    private readonly ComplexMatrix reducedState;

    internal PairEntanglementMetric(
        int firstQubitId, int secondQubitId, ComplexMatrix reducedState,
        double negativity, double logarithmicNegativity,
        double vonNeumannEntropyBits, double mutualInformationBits)
    {
        FirstQubitId = firstQubitId;
        SecondQubitId = secondQubitId;
        this.reducedState = reducedState;
        Negativity = negativity;
        LogarithmicNegativity = logarithmicNegativity;
        VonNeumannEntropyBits = vonNeumannEntropyBits;
        MutualInformationBits = mutualInformationBits;
    }

    public int FirstQubitId { get; }
    public int SecondQubitId { get; }
    public double Negativity { get; }
    public double LogarithmicNegativity { get; }
    public double VonNeumannEntropyBits { get; }
    public double MutualInformationBits { get; }
    public bool IsEntangled => LogarithmicNegativity > EntanglementMetrics.EntanglementTolerance;
    public ComplexMatrix GetReducedStateCopy() => reducedState.Clone();
}

public static class EntanglementMetrics
{
    public const double EntanglementTolerance = 1e-7;
    public const double CorrelationEntropyThreshold = 1e-5;
    private const double LogTwo = 0.69314718055994530942;

    public static EntanglementSnapshot Build(
        ComplexMatrix globalState, int qubitCount, long stateVersion = 0)
    {
        DensityMatrixValidationResult validation =
            QuantumStateEngine.ValidateDensityMatrix(globalState, qubitCount, 1e-7);
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.Message, nameof(globalState));
        }

        var nodes = new QubitMetric[qubitCount];
        for (int qubitId = 0; qubitId < qubitCount; qubitId++)
        {
            ComplexMatrix reduced = QuantumStateEngine.PartialTrace(
                globalState, new[] { qubitId }, qubitCount);
            Vector3 bloch = BlochVector(reduced);
            double purity = ClampPhysical((reduced * reduced).Trace().Real, 0.0, 1.0);
            double renyi2 = -Math.Log(Math.Max(1e-15, purity));
            double vonNeumann = BinaryEntropyFromBlochRadius(bloch.magnitude);
            nodes[qubitId] = new QubitMetric(
                qubitId, reduced, bloch, purity, renyi2, vonNeumann);
        }

        var pairs = new PairEntanglementMetric[qubitCount * (qubitCount - 1) / 2];
        int pairIndex = 0;
        for (int first = 0; first < qubitCount; first++)
        {
            for (int second = first + 1; second < qubitCount; second++)
            {
                ComplexMatrix reduced = QuantumStateEngine.PartialTrace(
                    globalState, new[] { first, second }, qubitCount);
                ComplexMatrix partialTranspose = PartialTransposeSecondQubit(reduced);
                double traceNorm = HermitianTraceNorm(partialTranspose);
                double negativity = Math.Max(0.0, (traceNorm - 1.0) * 0.5);
                double logarithmicNegativity = Math.Log(Math.Max(1.0, traceNorm), 2.0);
                double pairEntropy = HermitianEntropyBits(reduced);
                double mutualInformation = Math.Max(0.0,
                    nodes[first].VonNeumannEntropyBits + nodes[second].VonNeumannEntropyBits - pairEntropy);

                pairs[pairIndex++] = new PairEntanglementMetric(
                    first, second, reduced, negativity, logarithmicNegativity,
                    pairEntropy, mutualInformation);
            }
        }

        bool threeParty = qubitCount == 3;
        double minimumNodeEntropy = double.PositiveInfinity;
        for (int i = 0; i < nodes.Length; i++)
        {
            minimumNodeEntropy = Math.Min(minimumNodeEntropy, nodes[i].Renyi2Entropy);
            threeParty &= nodes[i].Renyi2Entropy > CorrelationEntropyThreshold;
        }
        double strength = threeParty
            ? Math.Min(1.0, minimumNodeEntropy / LogTwo)
            : 0.0;
        double globalPurity = ClampPhysical((globalState * globalState).Trace().Real, 0.0, 1.0);

        return new EntanglementSnapshot(
            stateVersion, nodes, pairs, threeParty, strength, globalPurity, validation);
    }

    public static ComplexMatrix PartialTransposeSecondQubit(ComplexMatrix pairState)
    {
        if (pairState == null || pairState.Rows != 4 || pairState.Columns != 4)
        {
            throw new ArgumentException("Pair state must be a 4x4 matrix.", nameof(pairState));
        }

        var result = new ComplexMatrix(4, 4);
        for (int firstRow = 0; firstRow < 2; firstRow++)
        {
            for (int secondRow = 0; secondRow < 2; secondRow++)
            {
                for (int firstColumn = 0; firstColumn < 2; firstColumn++)
                {
                    for (int secondColumn = 0; secondColumn < 2; secondColumn++)
                    {
                        int sourceRow = firstRow * 2 + secondRow;
                        int sourceColumn = firstColumn * 2 + secondColumn;
                        int targetRow = firstRow * 2 + secondColumn;
                        int targetColumn = firstColumn * 2 + secondRow;
                        result[targetRow, targetColumn] = pairState[sourceRow, sourceColumn];
                    }
                }
            }
        }
        return result;
    }

    public static double HermitianTraceNorm(ComplexMatrix matrix)
    {
        double[] doubledSpectrum = RealSymmetricSpectrum(HermitianRealEmbedding(matrix));
        double traceNorm = 0.0;
        for (int i = 0; i < doubledSpectrum.Length; i++)
        {
            traceNorm += Math.Abs(doubledSpectrum[i]);
        }
        return traceNorm * 0.5;
    }

    public static double HermitianEntropyBits(ComplexMatrix densityMatrix)
    {
        double[] doubledSpectrum = RealSymmetricSpectrum(HermitianRealEmbedding(densityMatrix));
        double entropy = 0.0;
        for (int i = 0; i < doubledSpectrum.Length; i++)
        {
            double eigenvalue = doubledSpectrum[i];
            if (eigenvalue > 1e-14)
            {
                entropy -= 0.5 * eigenvalue * (Math.Log(eigenvalue) / LogTwo);
            }
        }
        return Math.Max(0.0, entropy);
    }

    private static Vector3 BlochVector(ComplexMatrix state)
    {
        return new Vector3(
            (float)(2.0 * state[0, 1].Real),
            (float)(-2.0 * state[0, 1].Imaginary),
            (float)(state[0, 0].Real - state[1, 1].Real));
    }

    private static double BinaryEntropyFromBlochRadius(double radius)
    {
        radius = ClampPhysical(radius, 0.0, 1.0);
        double high = (1.0 + radius) * 0.5;
        double low = (1.0 - radius) * 0.5;
        return EntropyTerm(high) + EntropyTerm(low);
    }

    private static double EntropyTerm(double probability)
    {
        return probability > 1e-15
            ? -probability * (Math.Log(probability) / LogTwo)
            : 0.0;
    }

    private static double[,] HermitianRealEmbedding(ComplexMatrix matrix)
    {
        if (matrix == null || matrix.Rows != matrix.Columns)
        {
            throw new ArgumentException("Hermitian spectrum requires a square matrix.", nameof(matrix));
        }

        int size = matrix.Rows;
        var embedded = new double[size * 2, size * 2];
        for (int row = 0; row < size; row++)
        {
            for (int column = 0; column < size; column++)
            {
                Complex value = matrix[row, column];
                embedded[row, column] = value.Real;
                embedded[row, column + size] = -value.Imaginary;
                embedded[row + size, column] = value.Imaginary;
                embedded[row + size, column + size] = value.Real;
            }
        }
        return embedded;
    }

    private static double[] RealSymmetricSpectrum(double[,] source)
    {
        int size = source.GetLength(0);
        var matrix = (double[,])source.Clone();
        int maxIterations = 64 * size * size;
        bool converged = false;
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            int p = 0;
            int q = 1;
            double maximum = 0.0;
            for (int row = 0; row < size; row++)
            {
                for (int column = row + 1; column < size; column++)
                {
                    double magnitude = Math.Abs(matrix[row, column]);
                    if (magnitude > maximum)
                    {
                        maximum = magnitude;
                        p = row;
                        q = column;
                    }
                }
            }
            if (maximum < 1e-13)
            {
                converged = true;
                break;
            }

            double app = matrix[p, p];
            double aqq = matrix[q, q];
            double apq = matrix[p, q];
            double tau = (aqq - app) / (2.0 * apq);
            double tangent = tau >= 0.0
                ? 1.0 / (tau + Math.Sqrt(1.0 + tau * tau))
                : -1.0 / (-tau + Math.Sqrt(1.0 + tau * tau));
            double cosine = 1.0 / Math.Sqrt(1.0 + tangent * tangent);
            double sine = tangent * cosine;

            for (int index = 0; index < size; index++)
            {
                if (index == p || index == q)
                {
                    continue;
                }
                double aip = matrix[index, p];
                double aiq = matrix[index, q];
                matrix[index, p] = matrix[p, index] = cosine * aip - sine * aiq;
                matrix[index, q] = matrix[q, index] = sine * aip + cosine * aiq;
            }

            matrix[p, p] = app - tangent * apq;
            matrix[q, q] = aqq + tangent * apq;
            matrix[p, q] = matrix[q, p] = 0.0;
        }

        if (!converged)
        {
            throw new InvalidOperationException("Hermitian eigenvalue iteration did not converge.");
        }

        var spectrum = new double[size];
        for (int i = 0; i < size; i++)
        {
            spectrum[i] = matrix[i, i];
        }
        Array.Sort(spectrum);
        return spectrum;
    }

    private static double ClampPhysical(double value, double minimum, double maximum)
    {
        return Math.Max(minimum, Math.Min(maximum, value));
    }
}

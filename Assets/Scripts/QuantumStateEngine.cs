using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Pure, scene-independent density-matrix engine. Qubit 0 is the left-most bit
/// in the computational basis: |q0 q1 ... q(n-1)>.
/// </summary>
public sealed class QuantumStateEngine
{
    private const double DefaultTolerance = 1e-9;
    private const int MaxDenseQubits = 12;
    private ComplexMatrix densityMatrix;

    public QuantumStateEngine(int qubitCount)
    {
        if (qubitCount < 1 || qubitCount > MaxDenseQubits)
        {
            throw new ArgumentOutOfRangeException(nameof(qubitCount),
                $"Dense simulation supports between 1 and {MaxDenseQubits} qubits.");
        }

        QubitCount = qubitCount;
        Dimension = 1 << qubitCount;
        densityMatrix = new ComplexMatrix(Dimension, Dimension);
        densityMatrix[0, 0] = Complex.One;
    }

    public int QubitCount { get; }
    public int Dimension { get; }
    public long StateVersion { get; private set; }
    public ComplexMatrix DensityMatrix => densityMatrix;

    public void SetDensityMatrix(ComplexMatrix state)
    {
        EnsureStateShape(state, QubitCount);
        DensityMatrixValidationResult validation = ValidateDensityMatrix(state, QubitCount);
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.Message, nameof(state));
        }

        densityMatrix = state.Clone();
        StateVersion++;
    }

    public void ApplySingleQubitGate(int qubitId, ComplexMatrix gate)
    {
        ComplexMatrix embedded = EmbedSingle(gate, qubitId, QubitCount);
        ApplyUnitary(embedded);
    }

    /// <summary>
    /// Applies a 4x4 gate whose local basis is |first,second>. The two targets
    /// may be non-adjacent and their argument order is significant.
    /// </summary>
    public void ApplyTwoQubitGate(int firstQubitId, int secondQubitId, ComplexMatrix gate)
    {
        EnsureQubitId(firstQubitId, QubitCount);
        EnsureQubitId(secondQubitId, QubitCount);
        if (firstQubitId == secondQubitId)
        {
            throw new ArgumentException("A two-qubit gate requires two different targets.");
        }
        EnsureShape(gate, 4, 4, nameof(gate));

        var embedded = new ComplexMatrix(Dimension, Dimension);
        int firstBit = BitPosition(firstQubitId, QubitCount);
        int secondBit = BitPosition(secondQubitId, QubitCount);
        for (int row = 0; row < Dimension; row++)
        {
            for (int column = 0; column < Dimension; column++)
            {
                if (!OtherBitsEqual(row, column, firstBit, secondBit, QubitCount))
                {
                    continue;
                }

                int localRow = (((row >> firstBit) & 1) << 1) | ((row >> secondBit) & 1);
                int localColumn = (((column >> firstBit) & 1) << 1) | ((column >> secondBit) & 1);
                embedded[row, column] = gate[localRow, localColumn];
            }
        }

        ApplyUnitary(embedded);
    }

    public void StepHeisenberg(IReadOnlyList<QubitPairCoupling> couplings, double deltaTime)
    {
        if (couplings == null)
        {
            throw new ArgumentNullException(nameof(couplings));
        }
        if (deltaTime < 0.0 || double.IsNaN(deltaTime) || double.IsInfinity(deltaTime))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        }
        if (deltaTime == 0.0 || couplings.Count == 0)
        {
            return;
        }

        ComplexMatrix hamiltonian = BuildHeisenbergHamiltonian(couplings, QubitCount);
        ComplexMatrix unitary = Gates.MatrixExponential(
            hamiltonian * (-Complex.ImaginaryOne * deltaTime), 24);
        ApplyUnitary(unitary);
    }

    public ComplexMatrix PartialTrace(params int[] keepQubitIds)
    {
        return PartialTrace(densityMatrix, keepQubitIds, QubitCount);
    }

    public MeasurementResult MeasureZ(int qubitId, double sample01)
    {
        EnsureQubitId(qubitId, QubitCount);
        if (sample01 < 0.0 || sample01 >= 1.0 || double.IsNaN(sample01))
        {
            throw new ArgumentOutOfRangeException(nameof(sample01), "Sample must be in [0, 1).");
        }

        int targetBit = BitPosition(qubitId, QubitCount);
        double probabilityZero = 0.0;
        for (int basis = 0; basis < Dimension; basis++)
        {
            if (((basis >> targetBit) & 1) == 0)
            {
                probabilityZero += densityMatrix[basis, basis].Real;
            }
        }

        probabilityZero = Math.Max(0.0, Math.Min(1.0, probabilityZero));
        int outcome = sample01 < probabilityZero ? 0 : 1;
        double probability = outcome == 0 ? probabilityZero : 1.0 - probabilityZero;
        if (probability <= DefaultTolerance)
        {
            throw new InvalidOperationException("The sampled measurement outcome has zero probability.");
        }

        var collapsed = new ComplexMatrix(Dimension, Dimension);
        for (int row = 0; row < Dimension; row++)
        {
            if (((row >> targetBit) & 1) != outcome)
            {
                continue;
            }
            for (int column = 0; column < Dimension; column++)
            {
                if (((column >> targetBit) & 1) == outcome)
                {
                    collapsed[row, column] = densityMatrix[row, column] / probability;
                }
            }
        }

        densityMatrix = Stabilize(collapsed);
        StateVersion++;
        return new MeasurementResult(qubitId, outcome, probability);
    }

    public double ComputeRenyi2Entropy(int qubitId)
    {
        ComplexMatrix reduced = PartialTrace(qubitId);
        double purity = (reduced * reduced).Trace().Real;
        purity = Math.Max(DefaultTolerance, Math.Min(1.0, purity));
        return -Math.Log(purity);
    }

    public DensityMatrixValidationResult ValidateState(double tolerance = DefaultTolerance)
    {
        return ValidateDensityMatrix(densityMatrix, QubitCount, tolerance);
    }

    public static ComplexMatrix EmbedSingle(ComplexMatrix operation, int targetQubitId, int qubitCount)
    {
        EnsureQubitId(targetQubitId, qubitCount);
        EnsureShape(operation, 2, 2, nameof(operation));
        int dimension = 1 << qubitCount;
        int targetBit = BitPosition(targetQubitId, qubitCount);
        var embedded = new ComplexMatrix(dimension, dimension);

        for (int row = 0; row < dimension; row++)
        {
            for (int column = 0; column < dimension; column++)
            {
                if ((row & ~(1 << targetBit)) != (column & ~(1 << targetBit)))
                {
                    continue;
                }
                embedded[row, column] = operation[(row >> targetBit) & 1, (column >> targetBit) & 1];
            }
        }
        return embedded;
    }

    public static ComplexMatrix BuildHeisenbergHamiltonian(
        IReadOnlyList<QubitPairCoupling> couplings, int qubitCount)
    {
        if (couplings == null)
        {
            throw new ArgumentNullException(nameof(couplings));
        }
        if (qubitCount < 1 || qubitCount > MaxDenseQubits)
        {
            throw new ArgumentOutOfRangeException(nameof(qubitCount));
        }

        int dimension = 1 << qubitCount;
        var result = new ComplexMatrix(dimension, dimension);
        ComplexMatrix x = Gates.PauliX();
        ComplexMatrix y = Gates.PauliY();
        ComplexMatrix z = Gates.PauliZ();

        for (int index = 0; index < couplings.Count; index++)
        {
            QubitPairCoupling coupling = couplings[index];
            EnsureQubitId(coupling.FirstQubitId, qubitCount);
            EnsureQubitId(coupling.SecondQubitId, qubitCount);
            if (coupling.FirstQubitId == coupling.SecondQubitId)
            {
                throw new ArgumentException("A Heisenberg coupling requires two different qubits.");
            }
            if (double.IsNaN(coupling.Strength) || double.IsInfinity(coupling.Strength))
            {
                throw new ArgumentException("Coupling strength must be finite.");
            }

            double scale = coupling.Strength / 4.0;
            result += scale * EmbedPairProduct(x, coupling.FirstQubitId, x, coupling.SecondQubitId, qubitCount);
            result += scale * EmbedPairProduct(y, coupling.FirstQubitId, y, coupling.SecondQubitId, qubitCount);
            result += scale * EmbedPairProduct(z, coupling.FirstQubitId, z, coupling.SecondQubitId, qubitCount);
        }
        return result;
    }

    public static ComplexMatrix PartialTrace(ComplexMatrix state, int[] keepQubitIds, int qubitCount)
    {
        EnsureStateShape(state, qubitCount);
        if (keepQubitIds == null || keepQubitIds.Length == 0)
        {
            throw new ArgumentException("At least one qubit must be kept.", nameof(keepQubitIds));
        }

        var keep = new HashSet<int>();
        for (int i = 0; i < keepQubitIds.Length; i++)
        {
            EnsureQubitId(keepQubitIds[i], qubitCount);
            if (!keep.Add(keepQubitIds[i]))
            {
                throw new ArgumentException("The keep set contains a duplicate qubit ID.", nameof(keepQubitIds));
            }
        }

        int reducedDimension = 1 << keepQubitIds.Length;
        int dimension = 1 << qubitCount;
        var reduced = new ComplexMatrix(reducedDimension, reducedDimension);
        for (int row = 0; row < dimension; row++)
        {
            for (int column = 0; column < dimension; column++)
            {
                if (!TracedBitsEqual(row, column, keep, qubitCount))
                {
                    continue;
                }
                int reducedRow = SelectBits(row, keepQubitIds, qubitCount);
                int reducedColumn = SelectBits(column, keepQubitIds, qubitCount);
                reduced[reducedRow, reducedColumn] += state[row, column];
            }
        }
        return reduced;
    }

    public static DensityMatrixValidationResult ValidateDensityMatrix(
        ComplexMatrix state, int qubitCount, double tolerance = DefaultTolerance)
    {
        EnsureQubitCount(qubitCount);
        if (state == null)
        {
            return DensityMatrixValidationResult.Invalid("Density matrix is null.");
        }
        int expected = 1 << qubitCount;
        if (state.Rows != expected || state.Columns != expected)
        {
            return DensityMatrixValidationResult.Invalid(
                $"Expected a {expected}x{expected} matrix for {qubitCount} qubits.");
        }
        if (tolerance <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }

        Complex trace = state.Trace();
        double traceError = Complex.Abs(trace - Complex.One);
        double hermiticityError = 0.0;
        for (int row = 0; row < expected; row++)
        {
            for (int column = 0; column < expected; column++)
            {
                Complex value = state[row, column];
                if (double.IsNaN(value.Real) || double.IsNaN(value.Imaginary) ||
                    double.IsInfinity(value.Real) || double.IsInfinity(value.Imaginary))
                {
                    return DensityMatrixValidationResult.Invalid("Density matrix contains a non-finite value.");
                }
                hermiticityError = Math.Max(hermiticityError,
                    Complex.Abs(value - Complex.Conjugate(state[column, row])));
            }
        }

        if (traceError > tolerance)
        {
            return DensityMatrixValidationResult.Invalid(
                $"Trace differs from one by {traceError:E3}.", traceError, hermiticityError);
        }
        if (hermiticityError > tolerance)
        {
            return DensityMatrixValidationResult.Invalid(
                $"Hermiticity error is {hermiticityError:E3}.", traceError, hermiticityError);
        }
        if (!IsPositiveSemidefinite(state, tolerance))
        {
            return DensityMatrixValidationResult.Invalid(
                "Density matrix is not positive semidefinite.", traceError, hermiticityError);
        }

        double purity = (state * state).Trace().Real;
        if (purity < (1.0 / expected) - tolerance || purity > 1.0 + tolerance)
        {
            return DensityMatrixValidationResult.Invalid(
                $"Purity {purity:G17} is outside the physical range.", traceError, hermiticityError, purity);
        }
        return DensityMatrixValidationResult.Valid(traceError, hermiticityError, purity);
    }

    private void ApplyUnitary(ComplexMatrix unitary)
    {
        EnsureShape(unitary, Dimension, Dimension, nameof(unitary));
        densityMatrix = Stabilize(unitary * densityMatrix * unitary.ConjugateTranspose());
        StateVersion++;
    }

    private static ComplexMatrix EmbedPairProduct(
        ComplexMatrix firstOperation, int firstQubitId,
        ComplexMatrix secondOperation, int secondQubitId,
        int qubitCount)
    {
        return EmbedSingle(firstOperation, firstQubitId, qubitCount) *
               EmbedSingle(secondOperation, secondQubitId, qubitCount);
    }

    private static ComplexMatrix Stabilize(ComplexMatrix state)
    {
        ComplexMatrix hermitian = (state + state.ConjugateTranspose()) * 0.5;
        double trace = hermitian.Trace().Real;
        if (Math.Abs(trace) <= DefaultTolerance)
        {
            throw new InvalidOperationException("Quantum state has zero trace.");
        }
        return hermitian / trace;
    }

    // LDL* factorisation specialised for a Hermitian positive-semidefinite matrix.
    private static bool IsPositiveSemidefinite(ComplexMatrix matrix, double tolerance)
    {
        int size = matrix.Rows;
        var lower = new Complex[size, size];
        var diagonal = new double[size];
        for (int k = 0; k < size; k++)
        {
            Complex pivot = matrix[k, k];
            for (int j = 0; j < k; j++)
            {
                pivot -= lower[k, j] * Complex.Conjugate(lower[k, j]) * diagonal[j];
            }
            if (Math.Abs(pivot.Imaginary) > tolerance || pivot.Real < -tolerance)
            {
                return false;
            }

            diagonal[k] = Math.Abs(pivot.Real) <= tolerance ? 0.0 : pivot.Real;
            lower[k, k] = Complex.One;
            for (int i = k + 1; i < size; i++)
            {
                Complex residual = matrix[i, k];
                for (int j = 0; j < k; j++)
                {
                    residual -= lower[i, j] * Complex.Conjugate(lower[k, j]) * diagonal[j];
                }
                if (diagonal[k] == 0.0)
                {
                    if (Complex.Abs(residual) > tolerance)
                    {
                        return false;
                    }
                    lower[i, k] = Complex.Zero;
                }
                else
                {
                    lower[i, k] = residual / diagonal[k];
                }
            }
        }
        return true;
    }

    private static bool OtherBitsEqual(int row, int column, int firstBit, int secondBit, int qubitCount)
    {
        int mask = ((1 << qubitCount) - 1) & ~(1 << firstBit) & ~(1 << secondBit);
        return (row & mask) == (column & mask);
    }

    private static bool TracedBitsEqual(int row, int column, HashSet<int> keep, int qubitCount)
    {
        for (int qubitId = 0; qubitId < qubitCount; qubitId++)
        {
            if (!keep.Contains(qubitId))
            {
                int bit = BitPosition(qubitId, qubitCount);
                if (((row >> bit) & 1) != ((column >> bit) & 1))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static int SelectBits(int basis, int[] qubitIds, int qubitCount)
    {
        int result = 0;
        for (int i = 0; i < qubitIds.Length; i++)
        {
            result = (result << 1) | ((basis >> BitPosition(qubitIds[i], qubitCount)) & 1);
        }
        return result;
    }

    private static int BitPosition(int qubitId, int qubitCount) => qubitCount - 1 - qubitId;

    private static void EnsureQubitId(int qubitId, int qubitCount)
    {
        EnsureQubitCount(qubitCount);
        if (qubitId < 0 || qubitId >= qubitCount)
        {
            throw new ArgumentOutOfRangeException(nameof(qubitId));
        }
    }

    private static void EnsureStateShape(ComplexMatrix state, int qubitCount)
    {
        EnsureQubitCount(qubitCount);
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }
        int dimension = 1 << qubitCount;
        EnsureShape(state, dimension, dimension, nameof(state));
    }

    private static void EnsureQubitCount(int qubitCount)
    {
        if (qubitCount < 1 || qubitCount > MaxDenseQubits)
        {
            throw new ArgumentOutOfRangeException(nameof(qubitCount));
        }
    }

    private static void EnsureShape(ComplexMatrix matrix, int rows, int columns, string parameterName)
    {
        if (matrix == null)
        {
            throw new ArgumentNullException(parameterName);
        }
        if (matrix.Rows != rows || matrix.Columns != columns)
        {
            throw new ArgumentException($"Matrix must be {rows}x{columns}.", parameterName);
        }
    }
}

public readonly struct QubitPairCoupling
{
    public QubitPairCoupling(int firstQubitId, int secondQubitId, double strength)
    {
        FirstQubitId = firstQubitId;
        SecondQubitId = secondQubitId;
        Strength = strength;
    }

    public int FirstQubitId { get; }
    public int SecondQubitId { get; }
    public double Strength { get; }
}

public readonly struct MeasurementResult
{
    public MeasurementResult(int qubitId, int outcome, double probability)
    {
        QubitId = qubitId;
        Outcome = outcome;
        Probability = probability;
    }

    public int QubitId { get; }
    public int Outcome { get; }
    public double Probability { get; }
}

public readonly struct DensityMatrixValidationResult
{
    private DensityMatrixValidationResult(
        bool isValid, string message, double traceError, double hermiticityError, double purity)
    {
        IsValid = isValid;
        Message = message;
        TraceError = traceError;
        HermiticityError = hermiticityError;
        Purity = purity;
    }

    public bool IsValid { get; }
    public string Message { get; }
    public double TraceError { get; }
    public double HermiticityError { get; }
    public double Purity { get; }

    public static DensityMatrixValidationResult Valid(double traceError, double hermiticityError, double purity)
    {
        return new DensityMatrixValidationResult(true, "Density matrix is valid.", traceError, hermiticityError, purity);
    }

    public static DensityMatrixValidationResult Invalid(
        string message, double traceError = double.PositiveInfinity,
        double hermiticityError = double.PositiveInfinity, double purity = double.NaN)
    {
        return new DensityMatrixValidationResult(false, message, traceError, hermiticityError, purity);
    }
}

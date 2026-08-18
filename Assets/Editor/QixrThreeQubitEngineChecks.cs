#if UNITY_EDITOR
using System;
using System.Numerics;
using UnityEditor;
using UnityEngine;

/// <summary>Deterministic Phase 1 checks; does not load or mutate a scene.</summary>
public static class QixrThreeQubitEngineChecks
{
    private const double Tolerance = 1e-8;

    [MenuItem("QIXR/Validation/Run Three-Qubit Engine Checks")]
    public static void Run()
    {
        try
        {
            CheckInitializationAndLocalGates();
            CheckNonAdjacentPairAndCompositeEvolution();
            CheckPartialTraceAndMeasurement();
            CheckTwoQubitRegression();
            CheckInvariantValidator();
            Debug.Log("[QIXR Phase 1] Three-qubit engine checks passed.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[QIXR Phase 1] Three-qubit engine checks failed: {exception.Message}");
            throw;
        }
    }

    private static void CheckInitializationAndLocalGates()
    {
        var engine = new QuantumStateEngine(3);
        AssertEqual("three-qubit dimension", engine.Dimension, 8);
        AssertNear("initial |000> population", engine.DensityMatrix[0, 0], Complex.One);

        engine.ApplySingleQubitGate(2, Gates.PauliX());
        AssertNear("X(Q2) creates |001>", engine.DensityMatrix[1, 1], Complex.One);
        AssertNear("X(Q2) does not change Q0", engine.PartialTrace(0)[0, 0], Complex.One);

        engine = new QuantumStateEngine(3);
        engine.ApplySingleQubitGate(0, Gates.Hadamard());
        AssertNear("H(Q0) |000><100|", engine.DensityMatrix[0, 4], new Complex(0.5, 0.0));
        AssertValid("local gate state", engine);
    }

    private static void CheckNonAdjacentPairAndCompositeEvolution()
    {
        var engine = new QuantumStateEngine(3);
        engine.ApplySingleQubitGate(0, Gates.PauliX());
        engine.StepHeisenberg(new[]
        {
            new QubitPairCoupling(0, 2, 0.7),
            new QubitPairCoupling(1, 2, -0.2)
        }, 0.02);
        AssertValid("simultaneous non-adjacent coupling", engine, 1e-7);

        ComplexMatrix hamiltonian = QuantumStateEngine.BuildHeisenbergHamiltonian(
            new[] { new QubitPairCoupling(0, 2, 1.0) }, 3);
        AssertMatrix("Heisenberg Hamiltonian Hermiticity",
            hamiltonian, hamiltonian.ConjugateTranspose());
    }

    private static void CheckPartialTraceAndMeasurement()
    {
        var ghzZero = CreateGhzState();
        ComplexMatrix q0 = ghzZero.PartialTrace(0);
        AssertNear("GHZ rho0[0,0]", q0[0, 0], new Complex(0.5, 0.0));
        AssertNear("GHZ rho0[1,1]", q0[1, 1], new Complex(0.5, 0.0));

        ComplexMatrix pair20 = ghzZero.PartialTrace(2, 0);
        AssertNear("ordered trace |00>", pair20[0, 0], new Complex(0.5, 0.0));
        AssertNear("ordered trace |11>", pair20[3, 3], new Complex(0.5, 0.0));

        MeasurementResult zero = ghzZero.MeasureZ(0, 0.25);
        AssertEqual("GHZ measurement zero", zero.Outcome, 0);
        AssertNear("GHZ zero probability", zero.Probability, 0.5);
        AssertNear("conditional Q1=0", ghzZero.PartialTrace(1)[0, 0], Complex.One);
        AssertNear("conditional Q2=0", ghzZero.PartialTrace(2)[0, 0], Complex.One);

        var ghzOne = CreateGhzState();
        MeasurementResult one = ghzOne.MeasureZ(0, 0.75);
        AssertEqual("GHZ measurement one", one.Outcome, 1);
        AssertNear("conditional Q1=1", ghzOne.PartialTrace(1)[1, 1], Complex.One);
        AssertNear("conditional Q2=1", ghzOne.PartialTrace(2)[1, 1], Complex.One);
        AssertValid("measured GHZ", ghzOne);
    }

    private static QuantumStateEngine CreateGhzState()
    {
        var engine = new QuantumStateEngine(3);
        engine.ApplySingleQubitGate(0, Gates.Hadamard());
        engine.ApplyTwoQubitGate(0, 1, Cnot());
        engine.ApplyTwoQubitGate(0, 2, Cnot());
        return engine;
    }

    private static void CheckTwoQubitRegression()
    {
        var engine = new QuantumStateEngine(2);
        engine.ApplySingleQubitGate(0, Gates.PauliX());
        engine.StepHeisenberg(new[] { new QubitPairCoupling(0, 1, 1.0) }, 0.1);

        ComplexMatrix initial = Gates.DownMatrix().KroneckerProduct(Gates.UpMatrix());
        ComplexMatrix expectedUnitary = Gates.MatrixExponential(
            Gates.Hamiltonian2Spins(1f) * (-Complex.ImaginaryOne * 0.1), 24);
        ComplexMatrix expected = expectedUnitary * initial * expectedUnitary.ConjugateTranspose();
        AssertMatrix("two-qubit exchange regression", engine.DensityMatrix, expected, 1e-7);
    }

    private static void CheckInvariantValidator()
    {
        AssertValid("initial state", new QuantumStateEngine(3));

        var nonPositive = new ComplexMatrix(8, 8);
        nonPositive[0, 0] = 1.1;
        nonPositive[1, 1] = -0.1;
        DensityMatrixValidationResult result =
            QuantumStateEngine.ValidateDensityMatrix(nonPositive, 3);
        if (result.IsValid)
        {
            throw new InvalidOperationException("Validator accepted a matrix with a negative eigenvalue.");
        }
    }

    private static ComplexMatrix Cnot()
    {
        return ComplexMatrix.FromArray(new Complex[,]
        {
            { 1, 0, 0, 0 },
            { 0, 1, 0, 0 },
            { 0, 0, 0, 1 },
            { 0, 0, 1, 0 }
        });
    }

    private static void AssertValid(string label, QuantumStateEngine engine, double tolerance = Tolerance)
    {
        DensityMatrixValidationResult result = engine.ValidateState(tolerance);
        if (!result.IsValid)
        {
            throw new InvalidOperationException($"{label}: {result.Message}");
        }
    }

    private static void AssertMatrix(
        string label, ComplexMatrix actual, ComplexMatrix expected, double tolerance = Tolerance)
    {
        if (actual.Rows != expected.Rows || actual.Columns != expected.Columns)
        {
            throw new InvalidOperationException($"{label}: dimensions differ.");
        }
        for (int row = 0; row < actual.Rows; row++)
        {
            for (int column = 0; column < actual.Columns; column++)
            {
                AssertNear($"{label}[{row},{column}]", actual[row, column], expected[row, column], tolerance);
            }
        }
    }

    private static void AssertNear(string label, Complex actual, Complex expected, double tolerance = Tolerance)
    {
        if (Complex.Abs(actual - expected) > tolerance)
        {
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
        }
    }

    private static void AssertNear(string label, double actual, double expected, double tolerance = Tolerance)
    {
        if (Math.Abs(actual - expected) > tolerance)
        {
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
        }
    }

    private static void AssertEqual(string label, int actual, int expected)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
        }
    }
}
#endif

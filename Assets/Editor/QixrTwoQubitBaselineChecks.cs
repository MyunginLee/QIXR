#if UNITY_EDITOR
using System;
using System.Numerics;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Deterministic Phase 0 checks for the existing two-qubit math primitives.
/// Run from QIXR/Validation/Run Two-Qubit Math Baseline in the Unity editor.
/// This intentionally does not mutate a scene or the runtime QubitManager state.
/// </summary>
public static class QixrTwoQubitBaselineChecks
{
    private const double Tolerance = 1e-8;

    [MenuItem("QIXR/Validation/Run Two-Qubit Math Baseline")]
    public static void Run()
    {
        try
        {
            AssertUnitary("H", Gates.Hadamard());
            AssertUnitary("X", Gates.PauliX());
            AssertUnitary("Z", Gates.PauliZ());
            AssertUnitary("S", Gates.PhaseS());
            AssertMatrix("H|0><0|H†", Apply(Gates.Hadamard(), Gates.UpMatrix()), new ComplexMatrix(2, 2)
            {
                [0, 0] = 0.5,
                [0, 1] = 0.5,
                [1, 0] = 0.5,
                [1, 1] = 0.5
            });

            var initialTwoQubitState = Gates.UpMatrix().KroneckerProduct(Gates.DownMatrix());
            var exchangeUnitary = Gates.SpinExchange(1f, 0.1f);
            AssertUnitary("exchange U(J=1,t=0.1)", exchangeUnitary, 1e-6);
            var evolved = Apply(exchangeUnitary, initialTwoQubitState);
            AssertNear("exchange trace", evolved.Trace(), Complex.One, 1e-6);
            AssertMatrix("exchange Hermiticity", evolved, evolved.ConjugateTranspose(), 1e-6);

            Debug.Log("[QIXR Phase 0] Two-qubit math baseline passed: H/X/Z/S unitarity, H|0>, and exchange trace/Hermiticity.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[QIXR Phase 0] Two-qubit math baseline failed: {exception.Message}");
            throw;
        }
    }

    private static ComplexMatrix Apply(ComplexMatrix unitary, ComplexMatrix densityMatrix)
    {
        return unitary * densityMatrix * unitary.ConjugateTranspose();
    }

    private static void AssertUnitary(string label, ComplexMatrix matrix, double tolerance = Tolerance)
    {
        AssertMatrix(label, matrix * matrix.ConjugateTranspose(), ComplexMatrix.Identity(matrix.Rows), tolerance);
    }

    private static void AssertMatrix(string label, ComplexMatrix actual, ComplexMatrix expected, double tolerance = Tolerance)
    {
        if (actual.Rows != expected.Rows || actual.Columns != expected.Columns)
        {
            throw new InvalidOperationException($"{label}: matrix dimensions differ.");
        }

        for (int row = 0; row < actual.Rows; row++)
        {
            for (int column = 0; column < actual.Columns; column++)
            {
                AssertNear($"{label}[{row},{column}]", actual[row, column], expected[row, column], tolerance);
            }
        }
    }

    private static void AssertNear(string label, Complex actual, Complex expected, double tolerance)
    {
        if (Complex.Abs(actual - expected) > tolerance)
        {
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}, tolerance {tolerance}.");
        }
    }
}
#endif

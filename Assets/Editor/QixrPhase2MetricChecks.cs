#if UNITY_EDITOR
using System;
using Complex = System.Numerics.Complex;
using UnityEditor;
using UnityEngine;

public static class QixrPhase2MetricChecks
{
    private const double Tolerance = 1e-7;

    [MenuItem("QIXR/Validation/Run Phase 2 Entanglement Metrics")]
    public static void Run()
    {
        try
        {
            CheckProductState();
            CheckBellPairWithSpectator();
            CheckGhzState();
            CheckWState();
            CheckMeasurementRecomputesMetrics();
            Debug.Log("[QIXR Phase 2] Entanglement metric checks passed.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[QIXR Phase 2] Entanglement metric checks failed: {exception.Message}");
            throw;
        }
    }

    private static void CheckProductState()
    {
        var engine = new QuantumStateEngine(3);
        EntanglementSnapshot snapshot = Build(engine);
        for (int id = 0; id < 3; id++)
        {
            AssertNear($"product Q{id} radius", snapshot.GetNode(id).BlochRadius, 1.0);
            AssertNear($"product Q{id} S2", snapshot.GetNode(id).Renyi2Entropy, 0.0);
        }
        AssertAllPairsZero("product", snapshot);
        Assert(!snapshot.HasThreePartyCorrelation, "Product state incorrectly reports three-party correlation.");
    }

    private static void CheckBellPairWithSpectator()
    {
        QuantumStateEngine engine = CreateBellPairWithSpectator();
        EntanglementSnapshot snapshot = Build(engine);
        AssertNear("Bell Q0 radius", snapshot.GetNode(0).BlochRadius, 0.0);
        AssertNear("Bell Q1 radius", snapshot.GetNode(1).BlochRadius, 0.0);
        AssertNear("Bell spectator radius", snapshot.GetNode(2).BlochRadius, 1.0);

        PairEntanglementMetric pair01 = snapshot.GetPair(0, 1);
        AssertNear("Bell negativity", pair01.Negativity, 0.5);
        AssertNear("Bell logarithmic negativity", pair01.LogarithmicNegativity, 1.0);
        AssertNear("Bell mutual information", pair01.MutualInformationBits, 2.0);
        AssertNear("Bell Q0-Q2 logarithmic negativity", snapshot.GetPair(0, 2).LogarithmicNegativity, 0.0);
        AssertNear("Bell Q1-Q2 logarithmic negativity", snapshot.GetPair(1, 2).LogarithmicNegativity, 0.0);
        Assert(!snapshot.HasThreePartyCorrelation,
            "Bell pair plus spectator incorrectly reports three-party correlation.");
    }

    private static void CheckGhzState()
    {
        QuantumStateEngine engine = CreateGhzState();
        EntanglementSnapshot snapshot = Build(engine);
        for (int id = 0; id < 3; id++)
        {
            AssertNear($"GHZ Q{id} radius", snapshot.GetNode(id).BlochRadius, 0.0);
            AssertNear($"GHZ Q{id} S2", snapshot.GetNode(id).Renyi2Entropy, Math.Log(2.0));
        }
        AssertAllPairsZero("GHZ", snapshot);
        Assert(snapshot.HasThreePartyCorrelation, "GHZ state is missing the three-party correlation marker.");
        AssertNear("GHZ triad strength", snapshot.ThreePartyCorrelationStrength, 1.0);
        Assert(snapshot.IsGloballyPure, "GHZ test state should be globally pure.");
    }

    private static void CheckWState()
    {
        var engine = new QuantumStateEngine(3);
        var state = new ComplexMatrix(8, 8);
        int[] occupiedBasisStates = { 1, 2, 4 };
        for (int row = 0; row < occupiedBasisStates.Length; row++)
        {
            for (int column = 0; column < occupiedBasisStates.Length; column++)
            {
                state[occupiedBasisStates[row], occupiedBasisStates[column]] = new Complex(1.0 / 3.0, 0.0);
            }
        }
        engine.SetDensityMatrix(state);

        EntanglementSnapshot snapshot = Build(engine);
        Assert(snapshot.HasThreePartyCorrelation, "W state is missing three-party correlation.");
        for (int pairIndex = 0; pairIndex < snapshot.Pairs.Count; pairIndex++)
        {
            double logarithmicNegativity = snapshot.Pairs[pairIndex].LogarithmicNegativity;
            Assert(logarithmicNegativity > 0.49 && logarithmicNegativity < 0.51,
                $"W pair E_N expected approximately 0.498, got {logarithmicNegativity}.");
        }
    }

    private static void CheckMeasurementRecomputesMetrics()
    {
        QuantumStateEngine engine = CreateGhzState();
        long beforeMeasurement = engine.StateVersion;
        engine.MeasureZ(0, 0.25);
        Assert(engine.StateVersion > beforeMeasurement, "Measurement did not advance the state version.");
        EntanglementSnapshot snapshot = Build(engine);
        Assert(!snapshot.HasThreePartyCorrelation, "Measured GHZ still reports a triad.");
        AssertAllPairsZero("measured GHZ", snapshot);
        for (int id = 0; id < 3; id++)
        {
            AssertNear($"measured GHZ Q{id} radius", snapshot.GetNode(id).BlochRadius, 1.0);
        }
    }

    private static QuantumStateEngine CreateBellPairWithSpectator()
    {
        var engine = new QuantumStateEngine(3);
        engine.ApplySingleQubitGate(0, Gates.Hadamard());
        engine.ApplyTwoQubitGate(0, 1, Cnot());
        return engine;
    }

    private static QuantumStateEngine CreateGhzState()
    {
        QuantumStateEngine engine = CreateBellPairWithSpectator();
        engine.ApplyTwoQubitGate(0, 2, Cnot());
        return engine;
    }

    private static EntanglementSnapshot Build(QuantumStateEngine engine)
    {
        return EntanglementMetrics.Build(
            engine.DensityMatrix, engine.QubitCount, engine.StateVersion);
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

    private static void AssertAllPairsZero(string label, EntanglementSnapshot snapshot)
    {
        for (int pairIndex = 0; pairIndex < snapshot.Pairs.Count; pairIndex++)
        {
            AssertNear($"{label} pair {pairIndex} E_N",
                snapshot.Pairs[pairIndex].LogarithmicNegativity, 0.0);
        }
    }

    private static void AssertNear(string label, double actual, double expected)
    {
        if (Math.Abs(actual - expected) > Tolerance)
        {
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif

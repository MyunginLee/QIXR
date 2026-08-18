using System;
using UnityEngine;

public class FloorElectrons : MonoBehaviour
{
    private const int MaxShaderQubits = 8;
    private static readonly Color[] NodeColors =
    {
        new Color(0.10f, 0.80f, 1.00f, 1f),
        new Color(1.00f, 0.48f, 0.12f, 1f),
        new Color(0.90f, 0.25f, 0.95f, 1f)
    };

    public Material orbits;

    private Qubit[] qubits;
    private Vector4[] qubitPositions;
    private Vector4[] colors;
    private Vector4[] wavefunctionParameters;

    private void Start()
    {
        qubits = FindObjectsByType<Qubit>(FindObjectsSortMode.None);
        Array.Sort(qubits, (left, right) => left.GetIndex().CompareTo(right.GetIndex()));
        if (qubits.Length > MaxShaderQubits)
        {
            Debug.LogWarning($"Wave shader displays only the first {MaxShaderQubits} qubits.", this);
            Array.Resize(ref qubits, MaxShaderQubits);
        }

        qubitPositions = new Vector4[qubits.Length];
        colors = new Vector4[qubits.Length];
        wavefunctionParameters = new Vector4[qubits.Length];
        UpdateMaterial();
    }

    private void Update()
    {
        UpdateMaterial();
    }

    private void UpdateMaterial()
    {
        if (orbits == null || qubits == null)
        {
            return;
        }

        EntanglementSnapshot snapshot = QubitManager.GetEntanglementSnapshot();

        for (int i = 0; i < qubits.Length; i++)
        {
            Qubit qubit = qubits[i];
            Vector3 position = qubit.transform.position;
            qubitPositions[i] = new Vector4(position.x, position.z, position.y, 0f);

            Color nodeColor = NodeColors[i % NodeColors.Length];
            QubitMetric node = snapshot != null && i < snapshot.Nodes.Count
                ? snapshot.GetNode(i)
                : null;
            bool correlated = node != null &&
                              node.Renyi2Entropy > EntanglementMetrics.CorrelationEntropyThreshold;
            colors[i] = correlated
                ? new Vector4(nodeColor.r * 0.45f, nodeColor.g * 0.45f, 1f, 1f)
                : (Vector4)nodeColor;

            float blochRadius = node?.BlochRadius ?? 1f;
            wavefunctionParameters[i] = new Vector4(1f, 0f, 0f, Mathf.Lerp(1.35f, 0.85f, blochRadius));
        }

        orbits.SetInt("_NumQubits", qubits.Length);
        orbits.SetVectorArray("_OrbitColor", colors);
        orbits.SetVectorArray("_Centers", qubitPositions);
        orbits.SetVectorArray("_WaveFunctionParams", wavefunctionParameters);
    }
}

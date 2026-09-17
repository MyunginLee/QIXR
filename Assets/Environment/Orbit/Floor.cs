using System;
using UnityEngine;

public class FloorElectrons : MonoBehaviour
{
    private const int MaxShaderQubits = 8;
    private static readonly Color PureColor = new Color(0.99f, 0.402f, 0.79f, 1f);
    private static readonly Color EntangledColor = new Color(0.1f, 0.1f, 0.9f, 1f);

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

        // Unity fixes a material array's capacity on its first upload.
        qubitPositions = new Vector4[MaxShaderQubits];
        colors = new Vector4[MaxShaderQubits];
        wavefunctionParameters = new Vector4[MaxShaderQubits];
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

            int id = qubit.GetIndex();
            QubitMetric node = snapshot != null && id < snapshot.Nodes.Count
                ? snapshot.GetNode(id)
                : null;
            bool isEntangled = node != null &&
                node.Renyi2Entropy > EntanglementMetrics.CorrelationEntropyThreshold;
            colors[i] = (Vector4)(isEntangled ? EntangledColor : PureColor);
            wavefunctionParameters[i] = new Vector4(1f, 0f, 0f, 1f);
        }

        orbits.SetInt("_NumQubits", qubits.Length);
        orbits.SetVectorArray("_OrbitColor", colors);
        orbits.SetVectorArray("_Centers", qubitPositions);
        orbits.SetVectorArray("_WaveFunctionParams", wavefunctionParameters);
    }
}

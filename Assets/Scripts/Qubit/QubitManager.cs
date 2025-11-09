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

    public static void ApplyPauliX(Qubit qubit)
    {
        densityMatrix = qubit.GetPauliX() * densityMatrix * qubit.GetPauliX();
    }

    public static void ApplyPauliZ(Qubit qubit)
    {
        densityMatrix = qubit.GetPauliZ() * densityMatrix * qubit.GetPauliZ();
    }

    public static void ApplyHadamard(Qubit qubit)
    {
        densityMatrix = qubit.GetHadamard() * densityMatrix * qubit.GetHadamard();
    }

    public static void ApplyPhaseGate(Qubit qubit)
    {
        densityMatrix = qubit.GetPhaseS() * densityMatrix * qubit.GetPhaseSDagger();
    }

    public static void Measure(int index)
    {
        Matrix<Complex32> measureMatrix;
        ndarray qubit = PartialTrace(index);
        int state = UnityEngine.Random.Range(0f,1f) <= ((Complex32)qubit[0,0]).Real ? 0 : 1;
        if (state == 0)
        {
            measureMatrix = 1/Mathf.Sqrt(((Complex32)qubit[0,0]).Real) * UpMatrix();
        }
        else
        {
            measureMatrix = 1/Mathf.Sqrt(((Complex32)qubit[1,1]).Real) * DownMatrix();
        }

        Matrix<Complex32> qubitMatrix = (index == 0) ? measureMatrix : IdentityMatrix();
        for(int i = 1; i < GetInitQubits(); i++)
        {
            qubitMatrix = qubitMatrix.KroneckerProduct(index == i ? measureMatrix : IdentityMatrix());
        }

        densityMatrix = qubitMatrix * densityMatrix * qubitMatrix;
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
                }
            }
        }

        return couplings;
    }

    private static int RemoveBit(int value, int bitPosition)
    {
        int lowerMask = (1 << bitPosition) - 1;
        int lower = value & lowerMask;
        int upper = value >> (bitPosition + 1);
        return (upper << bitPosition) | lower;
    }
}



using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
using System.Collections.Generic;
using System.Linq;
using NumpyDotNet;
using static Gates;
using System;
using NumpyLib;
using static Qubit;

using TMPro;
public class QubitManager : MonoBehaviour
{
    static private Matrix<Complex32> densityMatrix;
    static int numQubits = 0;
    static int initQubits = 0;
    private List<Qubit> allQubits = new List<Qubit>();
    private float time = 0f;
    public static float THRESHOLD_DISTANCE = 2.5f;
    public static float[] J;
    public TMP_Text textMeshPro;

    public static float volume = 0.8f;
    void Start() 
    {
        GameObject[] qubits = GameObject.FindGameObjectsWithTag("Qubit");

        J = new float[qubits.Length];
        foreach (GameObject qubit in qubits)
        {
            Qubit qubitComponent = qubit.GetComponent<Qubit>();
            allQubits.Add(qubitComponent);
        }

        // Invoke("ApplyGate", 1f);
    }

    void Update()
    {
        time += Time.deltaTime;
        J = CalculateProximity(allQubits, time, THRESHOLD_DISTANCE);
    }

    void ApplyGate() {
        ApplyHadamard(allQubits[0]);
        ApplySpinExchange(Mathf.PI/2, time);
    }

    public static void UpdateDensityMatrix()
    {
        if (densityMatrix == null)
        {
            densityMatrix = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
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

    public static Matrix<Complex32> GetDensityMatrix()
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
        // Debug.Log(densityMatrix);
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

    public static ndarray PartialTrace(int index)
    {
        List<int> result = new List<int>();
        List<int> qtrace = new List<int> { };
        List<int> sel = new List<int> { index };
        Complex32[,] array = densityMatrix.ToArray();
        int[] dims = new int[2 * numQubits];
        int nd = numQubits;

        for (int i = 0; i < 2 * numQubits; i++)
        {
            dims[i] = 2;
        }
        for (int i = 0; i < nd; i++)
        {
            if (i != index)
            {
                qtrace.Add(i);
            }
        }
        //Based on Qutip's partial trace
        result.AddRange(qtrace);
        result.AddRange(qtrace.Select(q => nd + q));
        result.AddRange(sel);
        result.AddRange(sel.Select(q => nd + q));
        long[] positions = result.Select(i => (long)i).ToArray();

        ndarray matrix = np.array(array) / np.trace(np.array(array));

        ndarray rhomat = np.trace(matrix.reshape(new shape(dims))
                        .Transpose(positions));
        while (rhomat.shape != new shape(2, 2))
        {
            rhomat = np.trace(rhomat);
        }
        rhomat = rhomat / np.trace(rhomat);

        return rhomat;
    }

    // private static Matrix<Complex32> ConvertNdArrayToMatrix(ndarray toConvert)
    // {
    //     Complex32[,] converted = new Complex32[toConvert.shape[0], toConvert.shape[1]];
    //     for (int i = 0; i < toConvert.shape[0]; i++)
    //     {
    //         for (int j = 0; j < toConvert.shape[1]; j++)
    //         {
    //             converted[i, j] = (Complex32)toConvert[i, j];
    //         }
    //     }
    //     return Matrix<Complex32>.Build.DenseOfArray(converted);
    // }

    // private static ndarray ConvertMatrixToNdArray(Matrix<Complex32> matrix)
    // {
    //     Complex32[,] array = matrix.ToArray(); 
    //     return np.array(array);
    // }

    // public static (Matrix<Complex32> localDensityMatrix, ndarray q1Trace, ndarray q2Trace) ApplySpinExchange(float J, float time, ref ndarray q1Trace, ref ndarray q2Trace)
    // {
    //     Matrix<Complex32> localDensityMatrix; 
        
    //     Matrix<Complex32> q1TraceMatrix = ConvertNdArrayToMatrix(q1Trace);
    //     Matrix<Complex32> q2TraceMatrix = ConvertNdArrayToMatrix(q2Trace);

    //     localDensityMatrix = q1TraceMatrix;
    //     localDensityMatrix = localDensityMatrix.KroneckerProduct(q2TraceMatrix);

    //     Matrix<Complex32> U = SpinExchange(J, time);
    //     localDensityMatrix = U * localDensityMatrix * U.ConjugateTranspose();
    //     var subMatrices = BreakDownMatrix(localDensityMatrix);

    //     q1Trace = ConvertMatrixToNdArray(subMatrices[0]);
    //     q2Trace = ConvertMatrixToNdArray(subMatrices[1]);
    
    //     // Debug.Log("[+] LOG 1: SHAPE = (" + a.shape[0] + ", " + a.shape[1] + ")\tDATA TYPE: " + a.GetType());
    //     Debug.Log("DS: " + localDensityMatrix);
    //     Debug.Log("q1trace" + q1Trace + "q2trace" + q2Trace);
    //     // Debug.Log(q2Trace);

    //     return (localDensityMatrix, q1Trace, q2Trace);
    // }

    public static void ApplySpinExchange (float J, float time)
    {
        Matrix<Complex32> U = SpinExchange(J, time);
        densityMatrix = U * densityMatrix * U.ConjugateTranspose();
    }

    // private static Matrix<Complex32>[] BreakDownMatrix(Matrix<Complex32> combinedMatrix)
    // {
    //     var subMatrices = new Matrix<Complex32>[2];

    //     Complex32[,] subMatrix1 = new Complex32[2, 2];
    //     Complex32[,] subMatrix2 = new Complex32[2, 2];

    //     subMatrix1[0, 0] = combinedMatrix[0, 0] + combinedMatrix[2, 2];
    //     subMatrix1[0, 1] = combinedMatrix[0, 1] + combinedMatrix[2, 3];
    //     subMatrix1[1, 0] = combinedMatrix[1, 0] + combinedMatrix[3, 2];
    //     subMatrix1[1, 1] = combinedMatrix[1, 1] + combinedMatrix[3, 3];

    //     subMatrix2[0, 0] = combinedMatrix[0, 0] + combinedMatrix[1, 1];
    //     subMatrix2[0, 1] = combinedMatrix[0, 2] + combinedMatrix[1, 3];
    //     subMatrix2[1, 0] = combinedMatrix[2, 0] + combinedMatrix[3, 1];
    //     subMatrix2[1, 1] = combinedMatrix[2, 2] + combinedMatrix[3, 3];

    //     subMatrices[0] = Matrix<Complex32>.Build.DenseOfArray(subMatrix1); 
    //     subMatrices[1] = Matrix<Complex32>.Build.DenseOfArray(subMatrix2); 

    //     return subMatrices;
    // }


    float[] CalculateProximity(List<Qubit> qList, float time, float THRESHOLD_DISTANCE)
    {
        float[] J = new float[qList.Count];
        for (int i = 0; i < qList.Count; i++) 
        {
            for (int j = i+1; j < qList.Count; j++) 
            {
                Qubit qubitA = qList[i];
                Qubit qubitB = qList[j];
                float distance;
                float scalingFactor;
                float Jmax = 1f;

                distance = Vector3.Distance(qubitA.transform.position, qubitB.transform.position);
                if (distance <= THRESHOLD_DISTANCE) 
                {
                    scalingFactor = distance/THRESHOLD_DISTANCE;
                    //J[i] = Mathf.PI * scalingFactor;
                    J[i] = Jmax / 2f * (1f + (float)Math.Tanh(THRESHOLD_DISTANCE/2f) - distance);
                    J[j] = J[i];
                    ApplySpinExchange(J[i], time);
                }
            }
        }
        return J;
    }
}
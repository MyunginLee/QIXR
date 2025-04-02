using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
using System.Collections.Generic;
using System.Linq;
using NumpyDotNet;
using static Gates;
using NumpyLib;
using static Qubit;
using System;

using System.IO;
using System.Text.RegularExpressions;

using TMPro;
public class QubitManager : MonoBehaviour
{
    static private Matrix<Complex32> densityMatrix;
    static int numQubits = 0;
    static int initQubits = 0;
    private List<Qubit> allQubits = new List<Qubit>();
    private float time = 1f;
    public static float THRESHOLD_DISTANCE = 2f;

    private string filePath;
    // private StreamWriter writer;
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

        Invoke("ApplyGate", 0.5f);
        InvokeRepeating(nameof(UpdateSpinExchange), 0.1f, 0.1f);

        //filePath = "/Users/ngocdinh/Downloads/QubitJan11.csv";
        //writer = new StreamWriter(filePath);
    }

    void Update()
    {
        // time += Time.deltaTime;
        // J = CalculateProximity(allQubits, time, THRESHOLD_DISTANCE);

        // Debug.Log(
        //     $"{GetDensityMatrix()}\n" +
        //     $"{PartialTrace(0)}\n" +
        //     $"{PartialTrace(1)}\n"
        // );
    }

    private void UpdateSpinExchange()
    {
        J = CalculateProximity(allQubits, time, THRESHOLD_DISTANCE);
    }

    void ApplyGate() {
        // ApplyHadamard(allQubits[0]);
        ApplyPauliX(allQubits[0]);
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

    // public static void ApplyTest()
    // {
    //     Matrix<Complex32> cx = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
    //     {
    //         { 1, 0, 0, 0 },
    //         { 0, 1, 0, 0 },
    //         { 0, 0, 0, 1 },
    //         { 0, 0, 1, 0 }
    //     });
    //     densityMatrix = cx * densityMatrix * cx;
    // }

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

    public static double Entropy(int index)
    {
        ndarray partialTrace = PartialTrace(index);
        Complex32 entropy = (Complex32)np.trace(np.square(partialTrace))[0];
        return (double)(-np.log(entropy.Real));
    }


    public static void ApplySpinExchange (float J, float time)
    {
        Matrix<Complex32> U = SpinExchange(J, time);
        densityMatrix = U * densityMatrix * U.ConjugateTranspose();
    }

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
                    J[i] = Jmax / 2f * (1f + (float)System.Math.Tanh(THRESHOLD_DISTANCE/2f) - distance);
                    J[j] = J[i]; 
                    ApplySpinExchange(J[i], time);
                    // qubitA.UpdatePosition();
                    // qubitB.UpdatePosition();
                    
                    // string densityMatrixStr = SerializeMatrix(GetDensityMatrix());
                    // string qubit1TraceStr = SerializeMatrix(PartialTrace(i));
                    // string qubit2TraceStr = SerializeMatrix(PartialTrace(j));
                    // string extractedValue = ExtractValue(densityMatrixStr);

                    // writer.WriteLine (
                    //     $"{extractedValue}," +
                    //     $"{J}"
                    // );

                    // Debug.Log(
                    //     $"{GetDensityMatrix()}\n" +
                    //     $"{PartialTrace(i)}\n" +
                    //     $"{PartialTrace(j)}\n" +
                    //     $"Distance: {distance}, J: {J}\n" +
                    //     $"Time: {time}"
                    // );
                }
            }
        }
        return J;
    }

    //below are functions to extract value into csv file
    // private void OnApplicationQuit()
    // {
    //     if (writer != null)
    //     {
    //         writer.Close();
    //     }
    // }

    // private string SerializeMatrix(object matrix)
    // {
    //     return matrix.ToString();
    // }

    // private string ExtractValue(string matrixLog)
    // {
    //     string[] rows = matrixLog.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

    //     if (rows.Length < 3)
    //     {
    //         return null;
    //     }

    //     string thirdRow = rows[3].Trim();

    //     string[] values = Regex.Split(thirdRow, @"\s+"); 

    //     if (values.Length < 3)
    //     {
    //         return null;
    //     }

    //     return values[4];
    // }
}
using UnityEngine;
using static QubitManager;

using System;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
using System.Collections.Generic;
using System.Linq;
using static Qubit;

using NumpyDotNet;

using System.IO;
using System.Text.RegularExpressions;


public class MatrixExample : MonoBehaviour
{
    [SerializeField] GameObject prefab;
    private List<Qubit> allQubits = new List<Qubit>();
    private float time = 1.0f;
    float normalizedTime;
    private float THRESHOLD_DISTANCE = 3f;
    // private Qubit qubit1;
    // private Qubit qubit2;
    // private Qubit qubit3;

    private string filePath;
    private StreamWriter writer;


    void Start()
    {
        //Docs : https://numerics.mathdotnet.com/Matrix
        Invoke("TryExamples", 0.5f);
        // qubit1 = Instantiate(prefab).GetComponent<Qubit>();
        // qubit2 = Instantiate(prefab).GetComponent<Qubit>();
        // qubit3 = Instantiate(prefab).GetComponent<Qubit>();
        allQubits.Add(Instantiate(prefab).GetComponent<Qubit>());
        allQubits.Add(Instantiate(prefab).GetComponent<Qubit>());
        // allQubits.Add(Instantiate(prefab).GetComponent<Qubit>());
        // time += Time.deltaTime;

        filePath = "/Users/ngocdinh/Downloads/QubitLog1.csv";
        writer = new StreamWriter(filePath);
        // writer.WriteLine("Timestamp,Density Matrix,Qubit 1,Qubit 2,Distance,J,K");
    }

    void TryExamples()
    {
        ApplyHadamard(allQubits[0]);
        
        float J = Mathf.PI;
        // Matrix<Complex32> abc = ApplySpinExchange(J, time, PartialTrace(0),PartialTrace(1));
    
        Debug.Log(GetDensityMatrix());
        Debug.Log(PartialTrace(0));
        Debug.Log(PartialTrace(1));
        // Debug.Log(PartialTrace(2));
    }

    void CalculateProximity(List<Qubit> qList, float time, float THRESHOLD_DISTANCE) 
    {
        for (int i = 0; i < qList.Count-1; i++) 
        {
            for (int j = i+1; j < qList.Count; j++) 
            {
                Qubit qubitA;
                Qubit qubitB;
                float distance;
                float scalingFactor;
                float J;

                qubitA = qList[i];
                qubitB = qList[j];

                distance = Vector3.Distance(qubitA.transform.position, qubitB.transform.position);
                if (distance <= THRESHOLD_DISTANCE) 
                {
                    scalingFactor = distance/THRESHOLD_DISTANCE;
                    J = Mathf.PI * scalingFactor;
                    ApplySpinExchange(J, time);

                    string densityMatrixStr = SerializeMatrix(GetDensityMatrix());
                    string qubit1TraceStr = SerializeMatrix(PartialTrace(i));
                    string qubit2TraceStr = SerializeMatrix(PartialTrace(j));
                    string extractedValue = ExtractThirdValueOfThirdRow(densityMatrixStr);

                    // writer.WriteLine(
                    //     $"{time}," +
                    //     $"\"{densityMatrixStr}\"," +
                    //     $"\"{qubit1TraceStr}\"," +
                    //     $"\"{qubit2TraceStr}\"," +
                    //     $"{distance}," +
                    //     $"{J}" 
                    // );

                    writer.WriteLine (
                        $"{extractedValue}," +
                        $"{J}"
                    );

                    Debug.Log(
                        $"{GetDensityMatrix()}\n" +
                        $"{PartialTrace(i)}\n" +
                        $"{PartialTrace(j)}\n" +
                        $"{extractedValue}\n" +
                        $"Distance: {distance}, J: {J}\n" +
                        $"Time: {time}"
                    );
                }
            }
        }
    }

    void Update()
    {
        CalculateProximity(allQubits, time, THRESHOLD_DISTANCE);
        // time += Time.deltaTime;
        // normalizedTime = time % (2 * Mathf.PI);
    }

    private void OnApplicationQuit()
    {
        if (writer != null)
        {
            writer.Close();
        }
    }

    private string SerializeMatrix(object matrix)
    {
        // This function converts a matrix or trace object into a string representation
        return matrix.ToString();
    }

    private string ExtractThirdValueOfThirdRow(string matrixLog)
    {
        // Step 1: Split the matrix string into rows based on newlines
        string[] rows = matrixLog.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Step 2: Identify the 3rd row (index 2 in zero-based array)
        if (rows.Length < 3)
        {
            Debug.LogError("Matrix log does not have enough rows.");
            return null;
        }

        string thirdRow = rows[3].Trim();

        // Step 3: Use a regular expression to extract values from the 3rd row
        string[] values = Regex.Split(thirdRow, @"\s+"); // Split by spaces or tabs

        // Step 4: Check if the 3rd value exists (index 2 in zero-based array)
        if (values.Length < 3)
        {
            Debug.LogError("The third row does not contain enough values.");
            return null;
        }

        // Step 5: Return the 3rd value
        return values[4];
    }
}

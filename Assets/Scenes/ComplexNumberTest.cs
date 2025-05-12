using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
// using NumSharp;
using System.Collections.Generic;
using System.Linq;
using NumpyDotNet;
using static Gates;
using System;
using NumpyLib;
using static Qubit;

using TMPro;

public class ComplexNumberTest : MonoBehaviour
{
    public TMP_Text textMeshPro; // Drag your TextMeshPro object here

    void Update()
    {
        // Generate a new 3x3 matrix of Complex32 numbers with random values
        var matrix = Matrix<Complex32>.Build.Dense(3, 3, (i, j) =>
            new Complex32(UnityEngine.Random.Range(0f, 10f), UnityEngine.Random.Range(0f, 10f)));

        // Display the matrix in TextMeshPro
        textMeshPro.text = ComplexMatrixToString(matrix);
    }

    // Helper method to convert a complex matrix to a readable string
    string ComplexMatrixToString(Matrix<Complex32> matrix)
    {
        string matrixString = "";
        for (int i = 0; i < matrix.RowCount; i++)
        {
            for (int j = 0; j < matrix.ColumnCount; j++)
            {
                matrixString += $"{matrix[i, j].Real} + {matrix[i, j].Imaginary}i\t";
            }
            matrixString += "\n"; // Newline after each row
        }
        return matrixString;
    }
}

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
    public TMP_Text textMeshPro; 

    void Update()
    {
        var matrix = Matrix<Complex32>.Build.Dense(3, 3, (i, j) =>
            new Complex32(UnityEngine.Random.Range(0f, 10f), UnityEngine.Random.Range(0f, 10f)));

        textMeshPro.text = ComplexMatrixToString(matrix);
    }

    string ComplexMatrixToString(Matrix<Complex32> matrix)
    {
        string matrixString = "";
        for (int i = 0; i < matrix.RowCount; i++)
        {
            for (int j = 0; j < matrix.ColumnCount; j++)
            {
                matrixString += $"{matrix[i, j].Real} + {matrix[i, j].Imaginary}i\t";
            }
            matrixString += "\n"; 
        }
        return matrixString;
    }
}

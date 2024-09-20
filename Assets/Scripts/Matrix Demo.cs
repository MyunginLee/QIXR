using UnityEngine;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using static Gates;

public class MatrixExample : MonoBehaviour
{
    void Start()
    {
        //Docs : https://numerics.mathdotnet.com/Matrix

        //2x2 Matrix
        Matrix<double> Matrix2x2 = Matrix<double>.Build.DenseOfArray(new double[,]
        {
            { 1, 1},
            { 1, 1},
        });

        //4x4 Matrix
        Matrix<double> Matrix4x4 = Matrix<double>.Build.DenseOfArray(new double[,]
        {
            { 1, 1, 1, 1},
            { 1, 1, 1, 1},
            { 1, 1, 1, 1},
            { 1, 1, 1, 1}
        });

        //Showcasing Matrix of nxm 
        Matrix<double> Matrix4x5 = Matrix<double>.Build.DenseOfArray(new double[,]
        {
            { 1, 1, 1, 1, 2},
            { 1, 1, 1, 1, 2},
            { 1, 1, 1, 1, 2},
            { 1, 1, 1, 1, 2}
        });


        Debug.Log("Matrix2x2\n" + Matrix2x2.ToString());
        Debug.Log("Add\n" + (Matrix2x2 + Matrix2x2).ToString());
        Debug.Log("Subtract\n" + (Matrix2x2 - Matrix2x2).ToString());
        Debug.Log("Transpose\n" + (Matrix4x5.Transpose()).ToString());

        Matrix<Complex32> gateExample = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 0.63f, 0},
            { 0, 0.37f },
        });

        gateExample *= PauliX();

        Debug.Log(gateExample);

        var matrixA = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,] {
            {0.5f, 0},
            {0, 0.5f}
        });

        var matrixB = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,] {
            {0.5f, 0},
            {0, 0.5f}
        });

        Matrix<Complex32> tensorExample = matrixA.KroneckerProduct(matrixB);

        Debug.Log(tensorExample);
    }
}
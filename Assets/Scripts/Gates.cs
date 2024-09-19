using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
public class Gates : MonoBehaviour
{
    Complex32 i = Complex32.ImaginaryOne;
    void Start()
    {
        Matrix<Complex32> identityMatrix = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 1, 0 },
            { 0, 1 }
        });

        Matrix<Complex32> pauliX = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 0, 1 },
            { 1, 0 }
        });

        Matrix<Complex32> pauliY = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 0, -i },
            { i, 0 }
        });

        Matrix<Complex32> pauliZ = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 1, 0},
            { 0, -1},
        });

        Matrix<Complex32> hadamard = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 1, 1},
            { 1, -1},
        });
        hadamard.Multiply(1/Mathf.Sqrt(2));

    }
}
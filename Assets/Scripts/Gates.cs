using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
public class Gates : MonoBehaviour
{
    static private Complex32 i = Complex32.ImaginaryOne;

    static private Matrix<Complex32> identityMatrix = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 1, 0 },
            { 0, 1 }
        });
    static private Matrix<Complex32> pauliX = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 0, 1 },
            { 1, 0 }
        });
    static private Matrix<Complex32> pauliY = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 0, -i },
            { i, 0 }
        });
    static private Matrix<Complex32> pauliZ = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 1, 0},
            { 0, -1},
        });
    static private Matrix<Complex32> hadamard = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 1/Mathf.Sqrt(2), 1/Mathf.Sqrt(2)},
            { 1/Mathf.Sqrt(2), -(1/Mathf.Sqrt(2))},
        });
    public static Matrix<Complex32> IdentityMatrix()
    {
        return identityMatrix;
    }
    public static Matrix<Complex32> PauliX()
    {
        return pauliX;
    }
    public static Matrix<Complex32> PauliY()
    {
        return pauliY;
    }
    public static Matrix<Complex32> PauliZ()
    {
        return pauliX;
    }
    public static Matrix<Complex32> Hadamard()
    {
        return hadamard;
    }
}
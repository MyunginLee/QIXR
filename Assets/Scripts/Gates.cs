using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
public class Gates : MonoBehaviour
{
    Complex32 i = Complex32.ImaginaryOne;

    static private Matrix<Complex32> identityMatrix;
    static private Matrix<Complex32> pauliX;
    static private Matrix<Complex32> pauliY;
    static private Matrix<Complex32> pauliZ;
    static private Matrix<Complex32> hadamard;


    void Start()
    {
        identityMatrix = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 1, 0 },
            { 0, 1 }
        });

        pauliX = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 0, 1 },
            { 1, 0 }
        });

        pauliY = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 0, -i },
            { i, 0 }
        });

        pauliZ = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 1, 0},
            { 0, -1},
        });

        hadamard = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 1, 1},
            { 1, -1},
        });
        hadamard.Multiply(1/Mathf.Sqrt(2));
    }
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
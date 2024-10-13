using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
public class Gates : MonoBehaviour
{
    static private Complex32 i = Complex32.ImaginaryOne;

    static private Matrix<Complex32> upMatrix = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
    {
        { 1, 0 },
        { 0, 0 }
    });

    static private Matrix<Complex32> zeroMatrix = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
    {
        { 0, 0 },
        { 0, 0 }
    });

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

    static private Matrix<Complex32> phaseS = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 1, 0},
            { 0, i},
        });
    static private Matrix<Complex32> phaseSDagger = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { 1, 0},
            { 0, -i},
        });
    
    // hamilton matrix for 2 spins
    public static Matrix<Complex32> Hamiltonian2Spins(float J)
    {
        return Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
        {
            { J/2, 0, 0, 0 },
            { 0, -J/2, J/2, 0 },
            { 0, J/2, -J/2, 0 },
            { 0, 0, 0, J/2 }
        });
    }
    
    public static Matrix<Complex32> MatrixExponential(Matrix<Complex32> matrix, int terms = 10)
    {
        Matrix<Complex32> result = Matrix<Complex32>.Build.DenseIdentity(matrix.RowCount);
        Matrix<Complex32> term = result; 

        for (int i = 1; i <= terms; i++)
        {
            term = term * matrix / i; 
            result += term; 
        }

        return result;
    }

    // compute the time evolution operator
    public static Matrix<Complex32> SpinExchange(float J, float time)
    {
        Matrix<Complex32> H = Hamiltonian2Spins(J);
        Matrix<Complex32> identity = Matrix<Complex32>.Build.DenseIdentity(4);

        // calculate unitary evolution operator U(t) = exp(-iHt)
        Matrix<Complex32> exponent = -Complex32.ImaginaryOne * H * time; 
        return MatrixExponential(exponent); 
    }

    public static Matrix<Complex32> UpMatrix()
    {
        return upMatrix;
    }   
    public static Matrix<Complex32> ZeroMatrix()
    {
        return zeroMatrix;
    }   
    public static Matrix<Complex32> IdentityMatrix()
    {
        return identityMatrix;
    }
    public static Matrix<Complex32> PauliX()
    {
        return pauliX;
    }
    public static Matrix<Complex32> PauliZ()
    {
        return pauliZ;
    }
    public static Matrix<Complex32> Hadamard()
    {
        return hadamard;
    }

    public static Matrix<Complex32> PhaseS()
    {
        return phaseS;
    }
    public static Matrix<Complex32> PhaseSDagger()
    {
        return phaseSDagger;
    }
}
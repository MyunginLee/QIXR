using System;
using Complex = System.Numerics.Complex;
using UnityEngine;

public class Gates : MonoBehaviour
{
    private static readonly Complex i = Complex.ImaginaryOne;

    private static readonly ComplexMatrix upMatrix = ComplexMatrix.FromArray(new Complex[,]
    {
        { 1, 0 },
        { 0, 0 }
    });

    private static readonly ComplexMatrix downMatrix = ComplexMatrix.FromArray(new Complex[,]
    {
        { 0, 0 },
        { 0, 1 }
    });

    private static readonly ComplexMatrix zeroMatrix = ComplexMatrix.FromArray(new Complex[,]
    {
        { 0, 0 },
        { 0, 0 }
    });

    private static readonly ComplexMatrix identityMatrix = ComplexMatrix.Identity(2);

    private static readonly ComplexMatrix pauliX = ComplexMatrix.FromArray(new Complex[,]
    {
        { 0, 1 },
        { 1, 0 }
    });

    private static readonly ComplexMatrix pauliZ = ComplexMatrix.FromArray(new Complex[,]
    {
        { 1, 0 },
        { 0, -1 }
    });

    private static readonly ComplexMatrix hadamard = ComplexMatrix.FromArray(new Complex[,]
    {
        { 1 / Math.Sqrt(2.0), 1 / Math.Sqrt(2.0) },
        { 1 / Math.Sqrt(2.0), -1 / Math.Sqrt(2.0) }
    });

    private static readonly ComplexMatrix phaseS = ComplexMatrix.FromArray(new Complex[,]
    {
        { 1, 0 },
        { 0, i }
    });

    private static readonly ComplexMatrix phaseSDagger = ComplexMatrix.FromArray(new Complex[,]
    {
        { 1, 0 },
        { 0, -i }
    });

    public static ComplexMatrix Hamiltonian2Spins(float J)
    {
        return ComplexMatrix.FromArray(new Complex[,]
        {
            { J / 4f, 0, 0, 0 },
            { 0, -J / 4f, J / 2f, 0 },
            { 0, J / 2f, -J / 4f, 0 },
            { 0, 0, 0, J / 4f }
        });
    }

    public static ComplexMatrix MatrixExponential(ComplexMatrix matrix, int terms = 10)
    {
        if (matrix == null)
        {
            throw new ArgumentNullException(nameof(matrix));
        }

        var result = ComplexMatrix.Identity(matrix.Rows);
        var term = ComplexMatrix.Identity(matrix.Rows);

        for (int n = 1; n <= terms; n++)
        {
            term = term * matrix / n;
            result += term;
        }

        return result;
    }

    public static ComplexMatrix SpinExchange(float J, float time)
    {
        ComplexMatrix H = Hamiltonian2Spins(J);
        Complex scalar = -Complex.ImaginaryOne * time;
        ComplexMatrix exponent = H * scalar;
        return MatrixExponential(exponent);
    }

    public static ComplexMatrix UpMatrix()
    {
        return upMatrix;
    }

    public static ComplexMatrix DownMatrix()
    {
        return downMatrix;
    }

    public static ComplexMatrix ZeroMatrix()
    {
        return zeroMatrix;
    }

    public static ComplexMatrix IdentityMatrix()
    {
        return identityMatrix;
    }

    public static ComplexMatrix PauliX()
    {
        return pauliX;
    }

    public static ComplexMatrix PauliZ()
    {
        return pauliZ;
    }

    public static ComplexMatrix Hadamard()
    {
        return hadamard;
    }

    public static ComplexMatrix PhaseS()
    {
        return phaseS;
    }

    public static ComplexMatrix PhaseSDagger()
    {
        return phaseSDagger;
    }
}


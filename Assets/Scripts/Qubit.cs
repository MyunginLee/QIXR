using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
using static QubitManager;
using static Gates;
public class Qubit : MonoBehaviour
{
    private Matrix<Complex32> identityMatrix;
    private Matrix<Complex32> pauliX;
    private Matrix<Complex32> pauliZ;
    private Matrix<Complex32> hadamard;
    private Matrix<Complex32> phaseS;
    private Matrix<Complex32> phaseSDagger;

    // set default density matrix
    // private Matrix<Complex32> densityMatrix;

    // public void Initialize(string state)
    // {
    //     if (state == "0") // For |0⟩
    //     {
    //         densityMatrix = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
    //         {
    //             { 1, 0 },
    //             { 0, 0 }
    //         });
    //     }
    //     else if (state == "1") // For |1⟩
    //     {
    //         densityMatrix = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
    //         {
    //             { 0, 0 },
    //             { 0, 1 }
    //         });
    //     }
    //     else
    //     {
    //         Debug.LogError("Invalid state. Use '0' for |0⟩ or '1' for |1⟩.");
    //     }
    // }

    // public Matrix<Complex32> GetDensityMatrix()
    // {
    //     return densityMatrix;
    // }
    // end set density matrix

    private int initQubits;
    public void Awake()
    {
        IncrementInitQubits();
        int initQubits = GetInitQubits();

        identityMatrix = IdentityMatrix();
        pauliX = (initQubits == 1) ? PauliX() : IdentityMatrix();
        pauliZ = (initQubits == 1) ? PauliZ() : IdentityMatrix();
        hadamard = (initQubits == 1) ? Hadamard() : IdentityMatrix();
        phaseS = (initQubits == 1) ? PhaseS() : IdentityMatrix();
        phaseSDagger = (initQubits == 1) ? PhaseSDagger() : IdentityMatrix();

        for (int i = 2; i <= initQubits; i++)
        {
            identityMatrix = identityMatrix.KroneckerProduct(IdentityMatrix());
            pauliX = pauliX.KroneckerProduct(initQubits == i ? PauliX() : IdentityMatrix());
            pauliZ = pauliZ.KroneckerProduct(initQubits == i ? PauliZ() : IdentityMatrix());
            hadamard = hadamard.KroneckerProduct(initQubits == i ? Hadamard() : IdentityMatrix());
            phaseS = phaseS.KroneckerProduct(initQubits == i ? PhaseS() : IdentityMatrix());
            phaseSDagger = phaseSDagger.KroneckerProduct(initQubits == i ? PhaseSDagger() : IdentityMatrix());
        }

        for (int i = 0; i < GetQubits() - initQubits; i++)
        {
            identityMatrix = identityMatrix.KroneckerProduct(IdentityMatrix());
            pauliX = pauliX.KroneckerProduct(IdentityMatrix());
            pauliZ = pauliZ.KroneckerProduct(IdentityMatrix());
            hadamard = hadamard.KroneckerProduct(IdentityMatrix());
            phaseS = phaseS.KroneckerProduct(IdentityMatrix());
            phaseSDagger = phaseSDagger.KroneckerProduct(IdentityMatrix());
        }
    }

    public void Update()
    {
        if (initQubits != GetInitQubits())
        {
            initQubits += 1;
            identityMatrix = identityMatrix.KroneckerProduct(IdentityMatrix());
            pauliX = pauliX.KroneckerProduct(IdentityMatrix());
            pauliZ = pauliZ.KroneckerProduct(IdentityMatrix());
            hadamard = hadamard.KroneckerProduct(IdentityMatrix());
            phaseS = phaseS.KroneckerProduct(IdentityMatrix());
            phaseSDagger = phaseSDagger.KroneckerProduct(IdentityMatrix());
        }
    }


    public Matrix<Complex32> GetIdentityMatrix()
    {
        return identityMatrix;
    }
    public Matrix<Complex32> GetPauliX()
    {
        return pauliX;
    }
    public Matrix<Complex32> GetPauliZ()
    {
        return pauliZ;
    }
    public Matrix<Complex32> GetHadamard()
    {
        return hadamard;
    }

    public Matrix<Complex32> GetPhaseS()
    {
        return phaseS;
    }
    public Matrix<Complex32> GetPhaseSDagger()
    {
        return phaseSDagger;
    }

}
using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
using static QubitManager;
using static Gates;
public class Qubit : MonoBehaviour
{
    private Matrix<Complex32> identityMatrix = IdentityMatrix();
    private Matrix<Complex32> pauliX = PauliX();
    private Matrix<Complex32> pauliZ = PauliZ();
    private Matrix<Complex32> hadamard = Hadamard();
    private Matrix<Complex32> phaseS = PhaseS();
    private Matrix<Complex32> phaseSDagger = PhaseSDagger();

    private int initQubits;
    public void Start()
    {
        IncrementInitQubits();
        initQubits = GetInitQubits();
        for (int i = 1; i < initQubits; i++)
        {
            identityMatrix = IdentityMatrix().KroneckerProduct(identityMatrix);
            pauliX = IdentityMatrix().KroneckerProduct(pauliX);
            pauliZ = IdentityMatrix().KroneckerProduct(pauliZ);
            hadamard = IdentityMatrix().KroneckerProduct(hadamard);
            phaseS = IdentityMatrix().KroneckerProduct(phaseS);
            phaseSDagger = IdentityMatrix().KroneckerProduct(phaseSDagger);
        }

        for (int i = 1; i < GetQubits() - initQubits; i++)
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
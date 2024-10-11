using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
using static Gates;
public class QubitManager : MonoBehaviour
{
    static private Matrix<Complex32> densityMatrix = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
    {
        { 1, 0 },
        { 0, 0 }
    });
    static int numQubits = 0;
    static int initQubits = 0;

    public void Awake()
    {
        Qubit[] qubits = FindObjectsOfType<Qubit>();
        for (int i = 1; i < qubits.Length; i++)
        {
            densityMatrix = densityMatrix.KroneckerProduct(UpMatrix());
        }
        numQubits = qubits.Length;
    }

    public static Matrix<Complex32> GetDensityMatrix()
    {
        return densityMatrix;
    }
    public static int GetQubits()
    {
        return numQubits;
    }

    public static int GetInitQubits()
    {
        return initQubits;
    }

    public static void IncrementInitQubits()
    {
        initQubits += 1;
    }

    public static void ApplyGate(Matrix<Complex32> gate)
    {
        densityMatrix = gate * densityMatrix * gate;
    }

    public static void ApplyPhaseGate(Matrix<Complex32> phase, Matrix<Complex32> phaseDagger)
    {
        densityMatrix = phase * densityMatrix * phaseDagger;
    }

    public static void ApplySpinExchange(float J, float time)
    {
        Matrix<Complex32> U = SpinExchange(J, time);
        densityMatrix = U * densityMatrix * U.ConjugateTranspose(); 
    }
}
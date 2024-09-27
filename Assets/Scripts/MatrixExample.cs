using UnityEngine;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using static QubitManager;
public class MatrixExample : MonoBehaviour
{
    [SerializeField] private Qubit qubit1;
    [SerializeField] private Qubit qubit2;
    void Start()
    {
        //Docs : https://numerics.mathdotnet.com/Matrix
        
        Debug.Log(GetDensityMatrix());
        ApplyGate(qubit1.GetHadamard());
        Debug.Log(GetDensityMatrix());
    }
}
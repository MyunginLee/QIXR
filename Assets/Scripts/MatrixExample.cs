using UnityEngine;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using static QubitManager;
public class MatrixExample : MonoBehaviour
{
    [SerializeField] private Qubit qubit1;
    [SerializeField] private Qubit qubit2;
    [SerializeField] private Qubit qubit3;
    void Start()
    {
        //Docs : https://numerics.mathdotnet.com/Matrix
        
        // ApplyPhaseGate(qubit1.GetPhaseS(), qubit1.GetPhaseSDagger());
        // ApplyGate(qubit1.GetPauliX());
        ApplyGate(qubit1.GetHadamard());

        float J = 1.0f; 
        float time = 0.1f; 

        ApplySpinExchange(J, time);
    }

    void Update() {
        Debug.Log(GetDensityMatrix());
    }
}
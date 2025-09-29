using UnityEngine;
using static QubitManager;

using System.Collections.Generic;
using static Qubit;


public class MatrixExample : MonoBehaviour
{
    [SerializeField] GameObject prefab;
    private List<Qubit> allQubits = new List<Qubit>();
    private float time = 1f;
    private float THRESHOLD_DISTANCE = 3f;
    // private Qubit qubit1;
    // private Qubit qubit2;
    // private Qubit qubit3;


    void Start()
    {
        //Docs : https://numerics.mathdotnet.com/Matrix
        allQubits.AddRange(FindObjectsOfType<Qubit>());
        // Invoke("TryExamples", 3f);
        // qubit1 = Instantiate(prefab).GetComponent<Qubit>();
        // qubit2 = Instantiate(prefab).GetComponent<Qubit>();
        // qubit3 = Instantiate(prefab).GetComponent<Qubit>();
        // allQubits.Add(Instantiate(prefab).GetComponent<Qubit>());
        // allQubits.Add(Instantiate(prefab).GetComponent<Qubit>());
    }

    void Update()
    {
        // print(GetDensityMatrix());
        // print(PartialTrace(0));
        // print(PartialTrace(1));
    }

    void TryExamples()
    {

        // ApplyHadamard(allQubits[0]);
        // print(GetDensityMatrix());
        // print(cx * GetDensityMatrix() * cx);


        
        for(int i = 0; i < 1; i++)
        {
            // ApplyHadamard(allQubits[0]);
            // ApplyTest();
            // Measure(1);
            // print(PartialTrace(0));
            // print(PartialTrace(1));
            // print(GetDensityMatrix());
            allQubits[i].UpdatePosition();
        }

        // print("finsh");
        // float J = Mathf.PI;
        // Matrix<Complex32> abc = ApplySpinExchange(J, time, PartialTrace(0),PartialTrace(1));
        // Debug.Log(abc);
    
    }

    void CalculateProximity(List<Qubit> qList, float time, float THRESHOLD_DISTANCE) 
    {
        for (int i = 0; i < qList.Count-1; i++) 
        {
            for (int j = i+1; j < qList.Count; j++) 
            {
                Qubit qubitA;
                Qubit qubitB;
                float distance;
                float scalingFactor;
                float J;

                qubitA = qList[i];
                qubitB = qList[j];

                distance = Vector3.Distance(qubitA.transform.position, qubitB.transform.position);
                if (distance <= THRESHOLD_DISTANCE) 
                {
                    scalingFactor = distance/THRESHOLD_DISTANCE;
                    J = Mathf.PI * scalingFactor;
                    //ApplySpinExchange(J, time, PartialTrace(i),PartialTrace(j));
                    ApplySpinExchange(J, time);
                    // Debug.Log("Distance: " + distance + ", J: " + J);
                }
            }
        }
    }

    // void Update()
    // {
    //     CalculateProximity(allQubits, time, THRESHOLD_DISTANCE);
    // }
}




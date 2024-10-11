using UnityEngine;
using static QubitManager;
public class MatrixExample : MonoBehaviour
{
    [SerializeField] GameObject prefab;
    private Qubit qubit1;
    private Qubit qubit2;
    void Start()
    {
        //Docs : https://numerics.mathdotnet.com/Matrix
        Invoke("TryExamples", 0.5f);
        qubit1 = Instantiate(prefab).GetComponent<Qubit>();
        qubit2 = Instantiate(prefab).GetComponent<Qubit>();
    }
    void TryExamples()
    {
        ApplyHadamard(qubit1);
        float J = 1.0f; 
        float time = 0.1f; 
        ApplySpinExchange(J, time);

        Debug.Log(GetDensityMatrix());
        Debug.Log(PartialTrace(0));
        Debug.Log(PartialTrace(1));
    }
}
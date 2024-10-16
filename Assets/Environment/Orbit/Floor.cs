using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
using UnityEngine;

public class FloorElectrons : MonoBehaviour
{
    public static int QubitsCounts = 2;
    public Material orbits;  // Assign your material with the shader here
    Vector4[] qubitPosition;
    Vector4[] wavefunctionParameters;

    private GameObject[] qubits;
    Vector4[] colors;
    GameObject[] dot;
    void Start()
    {
        dot = GameObject.FindGameObjectsWithTag("Dot");
        qubits = GameObject.FindGameObjectsWithTag("Qubit");
        qubitPosition = new Vector4[qubits.Length];
        colors = new Vector4[qubits.Length];
        wavefunctionParameters = new Vector4[qubits.Length];
        // Assign init pos. x,z -> x, y in shader
        for (int i = 0; i < qubitPosition.Length; i++)
        {
            qubitPosition[i] = new Vector4(qubits[i].transform.position.x, qubits[i].transform.position.z, 0, 0);
            colors[i] = new Vector4(0.4f, -0.3f, 0.2f, 1.0f);
            wavefunctionParameters[i] = new Vector4(1f, 0f, 0f, 1f);
        }
        orbits.SetVectorArray("_OrbitColor", colors);
        orbits.SetVectorArray("_Centers", qubitPosition);
        orbits.SetInt("_NumQubits", qubitPosition.Length);
    }
    private void Update()
    {
        // Find qubits

        // Define the centers for the Gaussians
        for (int i = 0; i < qubitPosition.Length; i++)
        {
            qubitPosition[i] = new Vector4(qubits[i].transform.position.x, qubits[i].transform.position.z, qubits[i].transform.position.y, 0);

            Vector3 spin = (qubits[i].transform.position - dot[i].transform.position)/0.05f;
            Debug.Log(i + " " + spin);
            wavefunctionParameters[i] = new Vector4(Mathf.Abs(spin.y), Mathf.Abs(spin.x), Mathf.Abs(spin.x), 1f);
            //qubitPosition[i] = qubits[i].transform.position;
        }
        orbits.SetVectorArray("_OrbitColor", colors);
        orbits.SetVectorArray("_Centers", qubitPosition);
        orbits.SetVectorArray("_WaveFunctionParams", wavefunctionParameters);

    }
}

using UnityEngine;

public class FloorElectrons : MonoBehaviour
{
    public static int QubitsCounts = 2;
    public Material orbits;  // Assign your material with the shader here
    Vector4[] qubitPosition;
    private GameObject[] qubits;
    public Vector4[] colors; 
    void Start()
    {
        // Find qubits
        qubits = GameObject.FindGameObjectsWithTag("Qubit");
        qubitPosition = new Vector4[qubits.Length];
        colors = new Vector4[qubits.Length];
        // Assign init pos. x,z -> x, y in shader
        for (int i = 0; i < qubitPosition.Length; i++)
        {
            qubitPosition[i] = new Vector4(qubits[i].transform.position.x, qubits[i].transform.position.z, 0, 0);
            colors[i] = new Vector4(0.4f, -0.3f, 0.2f, 1.0f); 
        }
        orbits.SetVectorArray("_OrbitColor", colors);
        orbits.SetVectorArray("_Centers", qubitPosition);
        orbits.SetInt("_NumQubits", qubitPosition.Length);
    }
    private void Update()
    {

        // Define the centers for the Gaussians
        for (int i = 0; i < qubitPosition.Length; i++)
        {
            qubitPosition[i] = new Vector4(qubits[i].transform.position.x, qubits[i].transform.position.z, qubits[i].transform.position.y, 0);
            //qubitPosition[i] = qubits[i].transform.position;
        }
        orbits.SetVectorArray("_OrbitColor", colors);
        orbits.SetVectorArray("_Centers", qubitPosition);
    }
}

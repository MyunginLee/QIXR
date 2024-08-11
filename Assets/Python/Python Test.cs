using UnityEditor.Scripting.Python;
using UnityEditor;
using UnityEngine;
using System.IO;

public class PythonTest : MonoBehaviour
{
    private enum Gates
    {
        Hadamard, PauliX, PauliY, PauliZ, PhaseS, PhaseT
    }

    [SerializeField] private Gates dropdown;
    public void RunPython()
    {
        string scriptPath = Path.Combine(Application.dataPath, "python/test.py");
        PythonRunner.RunFile(scriptPath);
    }
}

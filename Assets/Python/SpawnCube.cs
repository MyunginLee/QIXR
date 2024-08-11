using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor.Scripting.Python;
using System.IO;
using System;
public class SpawnCube : MonoBehaviour
{

    [SerializeField] private InputActionAsset inputActions;

    private InputAction trigger;

    private GameObject cube; 

    private bool occurOnce = true; 

    void Start()
    {
        var actionMap = inputActions.FindActionMap("XRI LeftHand Interaction");
        trigger = actionMap.FindAction("Activate Value");
    }
    void Update()
    {
        if (trigger.ReadValue<float>() == 1 && occurOnce){
            Debug.Log("Occured");
            string scriptPath = Path.Combine(Application.dataPath, "python/SpawnCube.py");
            PythonRunner.RunFile(scriptPath);
            occurOnce = false;
        }
    }
    public void SpawnObject()
    {
        if (cube == null){
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        }
    }

}

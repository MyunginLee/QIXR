using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class WaveSimulatorInput : MonoBehaviour
{
    Vector4 mouse;
    WaveSimulator waveSimulator;

    public float inputRadius = 10.0f;

    WaveSimulatorMaterial waveSimulatorTextureToMaterial;

    void Start()
    {
        waveSimulator = GetComponent<WaveSimulator>();
        waveSimulatorTextureToMaterial = GetComponent<WaveSimulatorMaterial>();
    }


    void FixedUpdate()
    {

        waveSimulatorTextureToMaterial.material.SetVector(waveSimulatorTextureToMaterial.mouseName, mouse);
        waveSimulatorTextureToMaterial.material.SetFloat(waveSimulatorTextureToMaterial.radiusName, inputRadius);
    }

    void Update()
    {
        //UpdateMouseInput();
    }
}

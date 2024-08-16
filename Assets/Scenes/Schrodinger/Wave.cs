using UnityEngine;

public class Wave : MonoBehaviour
{
    public Material waveMaterial;
    public float timeSpeed = 50.0f;

    void Update()
    {
        float time = Time.time * timeSpeed;
        waveMaterial.SetFloat("_TimeScale", time);
    }
}

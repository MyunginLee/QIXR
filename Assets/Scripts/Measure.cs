using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

public class Measure : MonoBehaviour
{
    private GameObject qubit;
    private GameObject dot;
    private GameObject line;
    [SerializeField] private InputActionAsset inputActions;

    private float radius = 1;
    private float inclination;
    private float azimuth;
    private float timer;
    private void OnEnable()
    {
        inputActions.Enable();
    }

    void Awake()
    {
        qubit = gameObject.transform.parent.gameObject;
        dot = gameObject;
        line = gameObject.transform.GetChild(0).gameObject;
    }

    void Update()
    {

        if (timer > 0)
        {
            timer -= Time.deltaTime;
        }

        if (((inputActions.FindActionMap("Measure")).FindAction("Measurements")).ReadValue<float>() != 0)
        {
            float x = dot.transform.localPosition.x * 2;
            float y = dot.transform.localPosition.y * 2;
            float z = dot.transform.localPosition.z * 2;
            float xSq = Mathf.Pow(x, 2);
            float ySq = Mathf.Pow(y, 2);
            float zSq = Mathf.Pow(z, 2);
            radius = Mathf.Sqrt(xSq + ySq + zSq);
            inclination = Mathf.Acos(z / radius);
            azimuth = Mathf.Sign(y) * Mathf.Acos(x / Mathf.Sqrt(xSq + ySq));

            Debug.Log("X: " + x);
            Debug.Log("Y: " + y);
            Debug.Log("Z: " + z);

            Debug.Log("X_2: " + xSq);
            Debug.Log("Y_2: " + ySq);
            Debug.Log("Z_2: " + zSq);

            Debug.Log("Radius: " + radius);
            Debug.Log("Inclination: " + inclination);
            Debug.Log("Azimuth: " + azimuth);

            Debug.Log("Probability of 0: " + Mathf.Pow(Mathf.Abs(Mathf.Cos(inclination / 2)), 2));
            Debug.Log("Probability of 1: " + Mathf.Pow(Mathf.Abs(Mathf.Sin(inclination / 2)), 2));




        }

        if (((inputActions.FindActionMap("Measure")).FindAction("Qubit")).ReadValue<float>() != 0 && timer <= 0)
        {
            float random = Random.Range(0, 1f);
            if (random >= 0.5)
            {
                line.transform.localPosition = new Vector3(0, 0, -75);
                dot.transform.localPosition = new Vector3(0, 0, 0.5f);
            }
            else
            {
                line.transform.localPosition = new Vector3(0, 0, 75);
                dot.transform.localPosition = new Vector3(0, 0, -0.5f);
            }
            qubit.transform.rotation = Quaternion.Euler(-90, 0, 0);
            dot.transform.localRotation = Quaternion.identity;
            timer = 1f;

        }
    }

    public float GetAlpha()
    {
        return Mathf.Cos(Mathf.Acos(dot.transform.localPosition.z * 2 / radius) / 2);
    }

    public float GetBeta()
    {
        return Mathf.Sin(Mathf.Acos(dot.transform.localPosition.z * 2 / radius) / 2);
    }
}

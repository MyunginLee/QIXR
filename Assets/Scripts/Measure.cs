using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Measure : MonoBehaviour
{
    [SerializeField] private GameObject qubit;
    [SerializeField] private GameObject dot;
    [SerializeField] private GameObject line;
    [SerializeField] private InputActionAsset inputActions;

    private float radius;
    private float inclination; 
    private float azimuth;

    private float timer;

    private void OnEnable()
    {
        inputActions.Enable();
    }

    void Update()
    {

        if (timer > 0)
        {
            timer -= Time.deltaTime;
        }

        if (((inputActions.FindActionMap("Measure")).FindAction("Measurements")).ReadValue<float>() != 0)    
        {
            float x = dot.transform.position.x;
            float y = dot.transform.position.y;
            float z = dot.transform.position.z;
            float xSq = Mathf.Pow(x,2);
            float ySq = Mathf.Pow(y,2);
            float zSq = Mathf.Pow(z,2);
            radius = Mathf.Sqrt(xSq+ySq+zSq);
            inclination = Mathf.Acos(z/radius);
            azimuth = Mathf.Sign(y) * Mathf.Acos(x/Mathf.Sqrt(xSq+ySq));

            Debug.Log("X: " + x);
            Debug.Log("Y: " + y);
            Debug.Log("Z: " + z);

            Debug.Log("X_2: " + xSq);
            Debug.Log("Y_2: " + ySq);
            Debug.Log("Z_2: " + zSq);

            Debug.Log("Radius: " + radius);
            Debug.Log("Inclination: " + inclination);
            Debug.Log("Azimuth: " + azimuth);




        }

        if (((inputActions.FindActionMap("Measure")).FindAction("Qubit")).ReadValue<float>() != 0 && timer <= 0)
        {
            float random = Random.Range(0,1f);
            if ( random >= 0.5){
                line.transform.localPosition = new Vector3(0,0,-75);
                dot.transform.localPosition = new Vector3(0,0,0.5f);
            }
            else
            {
                line.transform.localPosition = new Vector3(0,0,75);
                dot.transform.localPosition = new Vector3(0,0,-0.5f);
            }
            qubit.transform.rotation = Quaternion.Euler(-90, 0, 0);
            dot.transform.localRotation = Quaternion.identity;
            timer = 1f;

        }    

    }
}

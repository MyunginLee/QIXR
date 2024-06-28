using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProximityChange : MonoBehaviour
{
    private Material sphereMaterial;
    private float minDistance = 1f;
    private float maxDistance = 3f;

    private Color startColor;
    private Color endColor = new Color(1f, 1f, 1f);

    // Start is called before the first frame update
    void Start()
    {
        sphereMaterial = GetComponent<MeshRenderer>().material;
        startColor = sphereMaterial.color;
    }

    // Update is called once per frame
    void Update()
    {
        GameObject[] qubits = GameObject.FindGameObjectsWithTag("Qubit");
        float closestDistance = float.MaxValue;

        foreach (GameObject qubit in qubits)
        {
            if (qubit == gameObject) continue;
            float distance = Vector3.Distance(transform.position, qubit.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
            }
        }

        closestDistance = Mathf.Clamp(closestDistance, minDistance, maxDistance);
        float t = Mathf.InverseLerp(maxDistance, minDistance, closestDistance);
        Color interpolatedColor = Color.Lerp(startColor, endColor, t);
        sphereMaterial.color = interpolatedColor;
    }
}

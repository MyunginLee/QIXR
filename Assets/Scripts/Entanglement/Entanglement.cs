using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[RequireComponent(typeof(AudioSource))]

public class Entanglement : MonoBehaviour
{
    private const float G = 500f;

    GameObject[] strings;

    BodyProperty[] bp;
    private int numberOfStrings = 300;
    TrailRenderer trailRenderer;
    GameObject[] qubits;
    GameObject[] shell;
    float qubitWeight = 10f; // qubit weight
    float stringWeight = 1f;
    public float trailtime = 1f;
    private bool[] entangled;
    AudioSource audioSource;
    private bool entangleTrigger = true;
    private bool untangleTrigger = true;

    [SerializeField]
    public AudioClip SoundUntangle, SoundEntangle;

    struct BodyProperty
    {
        public float mass;
        public Vector3 velocity;
        public Vector3 acceleration;
    }
    // Start is called before the first frame update
    void Start()
    {
        qubits = GameObject.FindGameObjectsWithTag("Qubit");
        shell = GameObject.FindGameObjectsWithTag("Shell");
        bp = new BodyProperty[numberOfStrings];
        strings = new GameObject[numberOfStrings];
        entangled = new bool[qubits.Length];
        audioSource = GetComponent<AudioSource>();

        for (int j = 0; j < qubits.Length; j++)
        {
            qubits[j].transform.localScale = new Vector3(1f, 1f, 1f) * 0.3f;
            shell[j].transform.position = qubits[j].transform.position;
        }

        for (int i = 0; i < numberOfStrings; i++)
        {
            strings[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strings[i].GetComponent<Collider>().isTrigger = true;
            float r = 10f;
            strings[i].transform.localScale = new Vector3(0.00001f, 0.00001f, 0.00001f);
            strings[i].transform.position = new Vector3(r * Mathf.Cos(Mathf.PI * 2 / numberOfStrings * i), r * Mathf.Sin(Mathf.PI * 2 / numberOfStrings * i), Random.Range(-10, 10));
            bp[i].velocity = new Vector3(r / 10f * Mathf.Sin(Mathf.PI * 2f / 3f * i), r / 10f * Mathf.Cos(Mathf.PI * 2f / 3f * i), 0);

            // trail
            trailRenderer = strings[i].AddComponent<TrailRenderer>();
            // Configure the TrailRenderer's properties
            trailRenderer.time = trailtime;  // Duration of the trail
            trailRenderer.startWidth = 0.003f;  // Width of the trail at the start
            trailRenderer.endWidth = 0.003f;    // Width of the trail at the end
            // a material to the trail
            trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
            // Set the trail color over time
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                //new GradientColorKey[] { new GradientColorKey(new Color((Mathf.Sin(Mathf.PI * 2f / numberOfStrings * i) + 1) / 2f, (Mathf.Cos(Mathf.PI * 2f / numberOfStrings * i) + 1) / 2f, Mathf.Tan(Mathf.PI * 2 / numberOfStrings * i)), 0.80f), new GradientColorKey(new Color((Mathf.Cos(Mathf.PI * 2 / numberOfStrings * i) + 1) / 5f, (Mathf.Sin(Mathf.PI * 2 / numberOfStrings * i) + 1) / 3f, Mathf.Tan(Mathf.PI * 2 / numberOfStrings * i)), 0.05f) },
                new GradientColorKey[] { new GradientColorKey(new Color(0f,0f, 0f), 0f), 
                                                        new GradientColorKey(new Color((float)(Mathf.Cos(Mathf.PI * 2f / numberOfStrings * i) + 1f) / 3f, 
                                                        0.3f - (float)( (Mathf.Cos(Mathf.PI * 2f / numberOfStrings * i) +1f) / 1.5f), 
                                                        0.5f+ (float)(Mathf.Sin(Mathf.PI * 2f / numberOfStrings * i) + 1f) / 3f), 
                                                        0.05f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.1f, 0.1f), new GradientAlphaKey(0.1f, 0.1f) }            );
            trailRenderer.colorGradient = gradient;
        }
    }

    void Update()
    {
        for (int i = 0; i < numberOfStrings; i++)
        {
            bp[i].acceleration = Vector3.zero;
        }

        // distance between qubits
        for (int j = 0; j < qubits.Length-1; j++)
        {
            float distance = (qubits[j].transform.position - qubits[j+1].transform.position).magnitude;
            if (distance <= QubitManager.THRESHOLD_DISTANCE)
            {
                entangled[j] = true;
                entangled[j+1] = true;
                //Debug.Log(j+ " " + distance + " " + QubitManager.J[j]);
            }
            else
            {
                entangled[j] = false;
                entangled[j + 1] = false;
            }
        }
        for (int j = 0; j < qubits.Length; j++)
        {
            float qubitScale = 0.3f;
            // Entangled logic.
            if (entangled[j])
            {           
                if(entangleTrigger == true)
                {
                    audioSource.pitch = Random.Range(0.8f, 1.3f);
                    audioSource.PlayOneShot(SoundEntangle, 1f);
                    entangleTrigger = !entangleTrigger;
                }
                untangleTrigger = true;

                qubitScale = QubitManager.J[j] / 3.15f * 0.3f;

                for (int i = 0; i < numberOfStrings; i++)
                {
                    trailRenderer.time = trailtime;
                    Vector3 distance = qubits[j].transform.position - strings[i].transform.position;
                    Vector3 gravity = CalculateGravity(distance, stringWeight, qubitWeight);

                    if (distance.sqrMagnitude > 0.05f)
                    {
                        bp[i].acceleration += gravity / stringWeight;
                        if (bp[i].acceleration.sqrMagnitude > 25)
                        {
                            bp[i].acceleration = bp[i].acceleration.normalized * 5f;
                        }
                        bp[i].velocity += bp[i].acceleration * Time.deltaTime;
                    }

                    if (bp[i].velocity.sqrMagnitude > QubitManager.J[j] * QubitManager.J[j])
                    {
                        bp[i].velocity = bp[i].velocity.normalized * QubitManager.J[j];
                    }
                    if (bp[i].velocity.sqrMagnitude > 36)
                    {
                        bp[i].velocity = bp[i].velocity.normalized * 6;
                    }

                    strings[i].transform.position += bp[i].velocity * Time.deltaTime;

                    // Trail color according to J
                    Gradient gradient = new Gradient();
                    gradient.SetKeys(
                        //new GradientColorKey[] { new GradientColorKey(new Color((Mathf.Sin(Mathf.PI * 2f / numberOfStrings * i) + 1) / 2f, (Mathf.Cos(Mathf.PI * 2f / numberOfStrings * i) + 1) / 2f, Mathf.Tan(Mathf.PI * 2 / numberOfStrings * i)), 0.80f), new GradientColorKey(new Color((Mathf.Cos(Mathf.PI * 2 / numberOfStrings * i) + 1) / 5f, (Mathf.Sin(Mathf.PI * 2 / numberOfStrings * i) + 1) / 3f, Mathf.Tan(Mathf.PI * 2 / numberOfStrings * i)), 0.05f) },
                        new GradientColorKey[] { new GradientColorKey(new Color(0f, 0f, 0f), 0f),
                            new GradientColorKey(new Color((float)(Mathf.Cos(Mathf.PI * 2f / (float)numberOfStrings * i) + 1f) / 3f,
                                                                   0.3f - (float)( (Mathf.Cos(Mathf.PI * 2f / (float)numberOfStrings * i) +1f )/ 10f),
                                                                   0.5f + (float) (Mathf.Sin(Mathf.PI * 2f / (float)numberOfStrings * i) +1f) / 9f),
                                                                   QubitManager.J[j] / 10f) },
                        new GradientAlphaKey[] { new GradientAlphaKey(0.1f, 0.1f),
                            new GradientAlphaKey(QubitManager.J[j] / 5f, QubitManager.J[j] / 5f) }
                    );
                    trailRenderer.colorGradient = gradient;
                }
            }
            else // unentangled logic
            {
                entangleTrigger = true; // this means ready for next trigger

                if (untangleTrigger == true)
                {
                    audioSource.pitch = Random.Range(0.8f, 1.3f);
                    audioSource.PlayOneShot(SoundUntangle, 0.5f);
                    untangleTrigger = !untangleTrigger;
                }
                for (int i = 0; i < numberOfStrings; i++)
                {
                    QubitManager.J[j] = 3.15f;
                    bp[i].velocity = Vector3.zero;
                    bp[i].acceleration = new Vector3(Random.Range(-0.01f, 0.01f), Random.Range(-0.01f, 0.01f), Random.Range(-0.01f, 0.01f));
                    bp[i].velocity += bp[i].acceleration * Time.deltaTime;
                    strings[i].transform.position += bp[i].velocity * Time.deltaTime;
                    //trailRenderer.time = trailRenderer.time * 0.3f; ;  // Duration of the trail

                }

            }
            shell[j].transform.position = qubits[j].transform.position;
            qubits[j].transform.localScale = new Vector3(qubitScale, qubitScale, qubitScale);

        }
    }
    private Vector3 CalculateGravity(Vector3 distanceVector, float m1, float m2)
    {
        Vector3 gravity; // note this is also Vector3
        gravity = G * m1 * m2 / (distanceVector.sqrMagnitude) * distanceVector.normalized;
        return gravity;
    }
}

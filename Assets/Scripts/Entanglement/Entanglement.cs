using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Qubit;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
using NumpyDotNet;

[RequireComponent(typeof(AudioSource))]

public class Entanglement : MonoBehaviour
{
    private const float G = 1f;

    GameObject[] strings;

    BodyProperty[] bp;
    private int numberOfStrings = 300;
    TrailRenderer[] trailRenderer;
    static public GameObject[] qubits;
    GameObject[] shell;
    float qubitWeight = 300f; // qubit weight
    float stringWeight = 1f;
    public float trailtime = 1f;
    static public bool[] entangled;
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
        trailRenderer = new TrailRenderer[numberOfStrings];
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
            strings[i].transform.position = new Vector3(r * Mathf.Cos(Mathf.PI * 2f / (float)numberOfStrings * (float)i), r * Mathf.Sin(Mathf.PI * 2f / (float)numberOfStrings * (float)i), Random.Range(-10f, 10f));
            bp[i].velocity = new Vector3(r / 10f * Mathf.Sin(Mathf.PI * 2f / 3f * (float)i), r / 10f * Mathf.Cos(Mathf.PI * 2f / 3f * (float)i), 0);

            // trail
            trailRenderer[i] = strings[i].AddComponent<TrailRenderer>();
            // Configure the TrailRenderer's properties
            trailRenderer[i].time = trailtime;  // Duration of the trail
            trailRenderer[i].startWidth = 0.008f;  // Width of the trail at the start
            trailRenderer[i].endWidth = 0.003f;    // Width of the trail at the end
            // a material to the trail
            trailRenderer[i].material = new Material(Shader.Find("Sprites/Default"));
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
            trailRenderer[i].colorGradient = gradient;
        }
    }

    // calculate qubit radius
    public float ComputeQubitScale(Qubit qubit)
    {
        if (QubitManager.GetDensityMatrix().ColumnCount > 2)
        {
            ndarray array = QubitManager.PartialTrace(qubit.index);
            float rho00 = ((Complex32)array[0, 0]).Real;
            float rho11 = ((Complex32)array[1, 1]).Real;
            float rho01 = ((Complex32)array[0, 1]).Real;
            float rho10 = ((Complex32)array[1, 0]).Real;

            float r = Mathf.Sqrt(Mathf.Pow(rho00 - rho11, 2) + 4 * rho01 * rho10);
            return Mathf.Clamp(r, 0f, 1f);  
        }
        return 1f; 
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
            // else
            // {
            //     entangled[j] = false;
            //     entangled[j + 1] = false;
            // }
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

                //qubitScale = QubitManager.J[j] / 3.15f * 0.3f;
                Qubit qubitComponent = qubits[j].GetComponent<Qubit>();
                qubitScale = ComputeQubitScale(qubitComponent) * 0.3f;
                float entropy = (float)QubitManager.entropy;

                for (int i = 0; i < numberOfStrings; i++)
                {
                    trailRenderer[i].time = trailtime;
                    Vector3 distance = qubits[j].transform.position - strings[i].transform.position;
                    Vector3 gravity = CalculateGravity(distance, stringWeight, qubitWeight);

                    if (distance.sqrMagnitude > 0.04f)
                    {
                        bp[i].acceleration += gravity / stringWeight;
                        // if (bp[i].acceleration.sqrMagnitude > 100f+ Random.Range(-4f, 4f))
                        if (bp[i].acceleration.sqrMagnitude > 9f)
                        {
                            bp[i].acceleration = bp[i].acceleration.normalized * (3f + Random.Range(-1f, 1f));
                        }
                        bp[i].velocity += bp[i].acceleration * Time.deltaTime;

                        // constrain the velocity when strings are too close to qubit
                        if(distance.magnitude < 1f){                           
                            if (bp[i].velocity.sqrMagnitude > 0.3f + entropy/5f)
                            {
                                bp[i].velocity = bp[i].velocity.normalized * (0.3f + entropy/5f);
                            }
                        }

                    }

                    strings[i].transform.position += bp[i].velocity * Time.deltaTime;

                    // Trail color according to J
                    Gradient gradient = new Gradient();
                    gradient.SetKeys(
                        //new GradientColorKey[] { new GradientColorKey(new Color((Mathf.Sin(Mathf.PI * 2f / numberOfStrings * i) + 1) / 2f, (Mathf.Cos(Mathf.PI * 2f / numberOfStrings * i) + 1) / 2f, Mathf.Tan(Mathf.PI * 2 / numberOfStrings * i)), 0.80f), new GradientColorKey(new Color((Mathf.Cos(Mathf.PI * 2 / numberOfStrings * i) + 1) / 5f, (Mathf.Sin(Mathf.PI * 2 / numberOfStrings * i) + 1) / 3f, Mathf.Tan(Mathf.PI * 2 / numberOfStrings * i)), 0.05f) },
                        new GradientColorKey[] { new GradientColorKey(new Color(0f, 0f, 0f), 0f),
                            new GradientColorKey(new Color((float)(entropy/4f+Mathf.Cos(Mathf.PI * 2f / (float)numberOfStrings * i) + 1f) / 5f,
                                                                   0.4f - entropy/10f - (float)( (Mathf.Cos(Mathf.PI * 2f / (float)numberOfStrings * i) +1f )/ 10f),
                                                                   0.5f - entropy/10f + (float) (Mathf.Sin(Mathf.PI * 2f / (float)numberOfStrings * i) +1f) / 9f),
                                                                   entropy / 4f) },
                            // new GradientColorKey(new Color((float)(QubitManager.J[j]/4f+Mathf.Cos(Mathf.PI * 2f / (float)numberOfStrings * i) + 1f) / 5f,
                            //                                        0.4f - QubitManager.J[j]/10f - (float)( (Mathf.Cos(Mathf.PI * 2f / (float)numberOfStrings * i) +1f )/ 10f),
                            //                                        0.5f - QubitManager.J[j]/10f + (float) (Mathf.Sin(Mathf.PI * 2f / (float)numberOfStrings * i) +1f) / 9f),
                            //                                        QubitManager.J[j] / 4f) },


                        new GradientAlphaKey[] { new GradientAlphaKey(entropy / 5f, entropy / 5f),
                            new GradientAlphaKey(entropy / 5f, entropy / 5f) }
                    );
                    trailRenderer[i].colorGradient = gradient;
                    trailRenderer[i].startWidth = entropy/3f;
                    trailRenderer[i].endWidth = entropy/5f;
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


            // Trail update according to J
            for (int i = 0; i < numberOfStrings; i++)
            {
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    //new GradientColorKey[] { new GradientColorKey(new Color((Mathf.Sin(Mathf.PI * 2f / numberOfStrings * i) + 1) / 2f, (Mathf.Cos(Mathf.PI * 2f / numberOfStrings * i) + 1) / 2f, Mathf.Tan(Mathf.PI * 2 / numberOfStrings * i)), 0.80f), new GradientColorKey(new Color((Mathf.Cos(Mathf.PI * 2 / numberOfStrings * i) + 1) / 5f, (Mathf.Sin(Mathf.PI * 2 / numberOfStrings * i) + 1) / 3f, Mathf.Tan(Mathf.PI * 2 / numberOfStrings * i)), 0.05f) },
                    new GradientColorKey[] { new GradientColorKey(new Color(0f,0f, 0f), 0f), 
                                                            new GradientColorKey(new Color( 0.5f*QubitManager.J[j] + (float)(Mathf.Cos(Mathf.PI * 2f / numberOfStrings * i) + 1f) / (3f), 
                                                            -0.5f*QubitManager.J[j]  + (float)( (Mathf.Cos(Mathf.PI * 2f / numberOfStrings * i) +1f) / 2f), 
                                                            0.5f+ 0.4f*QubitManager.J[j] + (float)(Mathf.Sin(Mathf.PI * 2f / numberOfStrings * i) + 1f) / (3f + 3f*QubitManager.J[j])), 
                                                            0.05f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.1f + QubitManager.J[j], 0.1f+ QubitManager.J[j]), new GradientAlphaKey(0.1f, 0.1f) }            );
                trailRenderer[i].colorGradient = gradient;
            }
        }
    }
    private Vector3 CalculateGravity(Vector3 distanceVector, float m1, float m2)
    {
        Vector3 gravity; // note this is also Vector3
        gravity = G * m1 * m2 / (distanceVector.sqrMagnitude) * distanceVector.normalized;
        return gravity;
    }
}

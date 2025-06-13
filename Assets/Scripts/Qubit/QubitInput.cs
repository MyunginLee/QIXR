using System.Net.NetworkInformation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using static QubitManager;
using System.Collections;
using System.Dynamic;
[RequireComponent(typeof(AudioSource))]

public class QubitInput : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private InputActionAsset measureActions;
    [SerializeField] private Qubit qubit;
    [SerializeField] private Transform innerSphere;
    [SerializeField] private XRGrabInteractable interactableObject;
    [SerializeField] private float rotationSpeed = 2000.0f;
    private InputAction rotate;
    private InputAction translate;
    private InputActionMap leftHand;
    private InputActionMap rightHand;
    private bool buttonReleased = true;
    private string grabbedObject = "";
    public float[] triggered;
    private Quaternion[] gatespin;
    // AudioSource audioSource;
    static int numberofCommands = 4;
    private float time;
    private float initAngle = 3.14f;
    // public AudioClip audioClipX, audioClipY, audioClipA, audioClipB;
    private GameObject[] gates, activeGates;
    private int activeGateIdx, nubmerofActiveGates;
    private int maxGates = 50;

    [SerializeField] private GameObject guide;
    private GameObject spawnedInstance;
    private bool prefabDestroyed = false;
    private float joystickThreshold = 0.1f;

    void Start()
    {
        triggered = new float[maxGates]; // number of commands
        gates = new GameObject[numberofCommands];
        activeGates = new GameObject[maxGates];
        gatespin = new Quaternion[maxGates];
        interactableObject.selectEntered.AddListener(OnGrab);
        interactableObject.selectExited.AddListener(OnRelease);
        // audioSource = GetComponent<AudioSource>();

        gates[0] = GameObject.Find("Gate 1");
        gates[1] = GameObject.Find("Gate 2");
        gates[2] = GameObject.Find("Gate 3");
        gates[3] = GameObject.Find("Gate 4");
        //for (int i = 0; i < numberofCommands; i++)
        //{
        //    gates[i].SetActive(false);
        //}

        if (guide != null && spawnedInstance == null)
        {
            Vector3 spawnPos = qubit.transform.position + new Vector3(0, 0.2f, 0);
            spawnedInstance = Instantiate(guide, spawnPos, Quaternion.identity);
            spawnedInstance.transform.SetParent(qubit.transform);
        }
    }

    private void OnEnable()
    {
        measureActions.Enable();
        var actionMap = inputActions.FindActionMap("XRI LeftHand Interaction");
        leftHand = inputActions.FindActionMap("XRI LeftHand Interaction");
        rightHand = inputActions.FindActionMap("XRI RightHand Interaction");
        rotate = leftHand.FindAction("Rotate Anchor");
        translate = rightHand.FindAction("Translate Anchor");
    }


    private void Update()
    {
        time += Time.deltaTime;
        for (int i = 0; i < numberofCommands; i++)
        {
            gatespin[i] = new Quaternion(180f* Mathf.Sin(time+ i * Mathf.Cos(time)), 
                                      180f* Mathf.Sin(time * Mathf.Sin(time)) , 
                                      180f* Mathf.Cos(time),1 );
        }

        if (grabbedObject == qubit.name)
        {
            Vector2 input = translate.ReadValue<Vector2>();

            if (!prefabDestroyed && input.magnitude > joystickThreshold)
            {
                if (spawnedInstance != null)
                {
                    Destroy(spawnedInstance);
                    prefabDestroyed = true;
                }
            }

            // innerSphere.transform.Rotate(Vector3.up, rotate.ReadValue<Vector2>().x * rotationSpeed * Time.deltaTime);
            // innerSphere.transform.Rotate(Vector3.right, -translate.ReadValue<Vector2>().y * rotationSpeed * Time.deltaTime);
            if (leftHand.FindAction("X").WasReleasedThisFrame() && buttonReleased)
            {
                Measure(qubit.GetIndex());
                // Entanglement.entangled[qubit.GetIndex()] = false;
                for (int i = 0; i < QubitManager.numQubits; i++){
                    Entanglement.entangled[i] = false;
                }

                // int c = 0;
                // ApplyPauliX(qubit);
                // qubit.UpdatePosition();
                // buttonReleased = false;
                // audioSource.PlayOneShot(audioClipX, 1f);
                // triggered[activeGateIdx] = initAngle; // can be used to draw the gates
                // activeGates[activeGateIdx] = Instantiate(gates[c], innerSphere.transform.position, Quaternion.identity);
                // gates[c].transform.position = innerSphere.transform.position;
                // gates[c].transform.rotation = gatespin[c];
                // nubmerofActiveGates++;
                // activeGateIdx++;
            }
            if (leftHand.FindAction("X").WasReleasedThisFrame())
            {
                buttonReleased = true;
            }
            // if (leftHand.FindAction("Y").WasPressedThisFrame() && buttonReleased)
            // {
            //     int c = 1;
            //     ApplyPauliZ(qubit);
            //     qubit.UpdatePosition();
            //     buttonReleased = false;
            //     audioSource.PlayOneShot(audioClipY, 1f);
            //     triggered[activeGateIdx] = initAngle;
            //     activeGates[activeGateIdx] = Instantiate(gates[c], innerSphere.transform.position, Quaternion.identity);
            //     gates[c].transform.position = innerSphere.transform.position;
            //     gates[c].transform.rotation = gatespin[c];
            //     nubmerofActiveGates++;
            //     activeGateIdx++;
            // }
            // if (leftHand.FindAction("Y").WasReleasedThisFrame())
            // {
            //     int c = 1;
            //     buttonReleased = true;
            // }
            if (rightHand.FindAction("A").WasPressedThisFrame() && buttonReleased)
            {
                Measure(qubit.GetIndex());
                for (int i = 0; i < QubitManager.numQubits; i++){
                    Entanglement.entangled[i] = false;
                }

                // int c = 2;
                // ApplyHadamard(qubit);
                // qubit.UpdatePosition();
                // buttonReleased = false;
                // audioSource.PlayOneShot(audioClipA, 1f) ;
                // Debug.Log("A");
                // triggered[activeGateIdx] = initAngle;
                // activeGates[activeGateIdx] = Instantiate(gates[c], innerSphere.transform.position, Quaternion.identity);
                // gates[c].transform.position = innerSphere.transform.position;
                // gates[c].transform.rotation = gatespin[c];
                // nubmerofActiveGates++;
                // activeGateIdx++;
            }
            if (rightHand.FindAction("A").WasReleasedThisFrame())
            {
                buttonReleased = true;
            }
            // if (rightHand.FindAction("B").WasPressedThisFrame() && buttonReleased)
            // {
            //     int c = 3;
            //     ApplyPhaseGate(qubit);
            //     qubit.UpdatePosition();
            //     buttonReleased = false;
            //     audioSource.PlayOneShot(audioClipB, 1f);
            //     Debug.Log("B");
            //     triggered[activeGateIdx] = initAngle;
            //     activeGates[activeGateIdx] = Instantiate(gates[c], innerSphere.transform.position, Quaternion.identity);
            //     gates[c].transform.position = innerSphere.transform.position;
            //     gates[c].transform.rotation = gatespin[c];
            //     nubmerofActiveGates++;
            //     activeGateIdx++;
            // }
            // if (rightHand.FindAction("B").WasReleasedThisFrame())
            // {
            //     int c = 3;
            //     buttonReleased = true;
            // }

            //Measurement
            // if(press){
            //     Measure(qubit.GetIndex());
            //     qubit.UpdatePosition();
            //     buttonReleased = false;
            //     Debug.Log($"Measured index : {qubit.GetIndex()}");
            // }
            // if(release){
            //     buttonReleased = true;
            // }

        }
        // loop maximized gates

        if (activeGateIdx > maxGates-10)
        {
            activeGateIdx = 0;
        }

        if(activeGates.Length > 0)
        {
            for (int i = 0; i < activeGates.Length; i++)
            {   // Gates rotate for about 3 sec and destroy
                if (triggered[i] > 0f)
                {
                    //Debug.Log(triggered[i]);
                    triggered[i] -= Time.deltaTime;
                    gatespin[i] = new Quaternion(180f * Mathf.Sin(triggered[i] + i * Mathf.Cos(triggered[i])),
                                                  180f * Mathf.Sin(triggered[i] * Mathf.Sin(triggered[i])),
                                                  180f * Mathf.Cos(triggered[i]), 1);
                    activeGates[i].transform.rotation = gatespin[i];
                    //Debug.Log(" " + gatespin[i]);
                }
                else
                {
                    Destroy(activeGates[i]);
                    nubmerofActiveGates--;
                }
            }
        }

        // // fix model gates position..
        // for (int i = 0; i < numberofCommands; i++)
        // {
        //     gates[i].transform.position = new Vector3(-3f + i*1.7f, 0.6f, 7f);
        // }


    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        grabbedObject = interactableObject.gameObject.name;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        grabbedObject = "";
    }
}
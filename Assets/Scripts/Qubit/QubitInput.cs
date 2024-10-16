using System.Net.NetworkInformation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using static QubitManager;
using System.Collections;
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
    public bool[] triggered;
    private Quaternion[] gatespin;
    AudioSource audioSource;
    static int numberofCommands = 4;
    private float time;

    [SerializeField]
    public AudioClip audioClipX, audioClipY, audioClipA, audioClipB;

    public GameObject[] gates;

    void Start()
    {
        triggered = new bool[numberofCommands]; // number of commands
        gates = new GameObject[numberofCommands];
        gatespin = new Quaternion[numberofCommands];
        interactableObject.selectEntered.AddListener(OnGrab);
        interactableObject.selectExited.AddListener(OnRelease);
        audioSource = GetComponent<AudioSource>();

        for (int i = 0; i < numberofCommands; i++)
        {
            gates[i].SetActive(false);
        }
    }

    private void OnEnable()
    {
        measureActions.Enable();
        var actionMap = inputActions.FindActionMap("XRI LeftHand Interaction");
        leftHand = inputActions.FindActionMap("XRI LeftHand Interaction");
        rightHand = inputActions.FindActionMap("XRI RightHand Interaction");
        rotate = actionMap.FindAction("Rotate Anchor");
        translate = actionMap.FindAction("Translate Anchor");
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
            innerSphere.transform.Rotate(Vector3.up, rotate.ReadValue<Vector2>().x * rotationSpeed * Time.deltaTime);
            innerSphere.transform.Rotate(Vector3.right, -translate.ReadValue<Vector2>().y * rotationSpeed * Time.deltaTime);
            if (leftHand.FindAction("X").WasReleasedThisFrame() && buttonReleased)
            {
                int c = 0;
                ApplyPauliX(qubit);
                qubit.UpdatePosition();
                buttonReleased = false;
                audioSource.PlayOneShot(audioClipX, 1f);
                triggered[c] = true; // can be used to draw the gates
                Instantiate(gates[c], innerSphere.transform.position, Quaternion.identity);
                gates[c].transform.position = innerSphere.transform.position;
                gates[c].transform.rotation = gatespin[c];
            }
            if (leftHand.FindAction("X").WasReleasedThisFrame())
            {
                int c = 0;
                buttonReleased = true;
                triggered[c] = false;
                //gates[c].SetActive(triggered[c]);
            }
            if (leftHand.FindAction("Y").WasPressedThisFrame() && buttonReleased)
            {
                int c = 1;
                ApplyPauliZ(qubit);
                qubit.UpdatePosition();
                buttonReleased = false;
                audioSource.PlayOneShot(audioClipY, 1f);
                triggered[c] = true;
                Instantiate(gates[c], innerSphere.transform.position, Quaternion.identity);
                gates[c].transform.position = innerSphere.transform.position;
                gates[c].transform.rotation = gatespin[c];
            }
            if (leftHand.FindAction("Y").WasReleasedThisFrame())
            {
                int c = 1;
                buttonReleased = true;
                triggered[c] = false;
                //gates[c].SetActive(triggered[c]);
            }
            if (rightHand.FindAction("A").WasPressedThisFrame() && buttonReleased)
            {
                int c = 2;
                ApplyHadamard(qubit);
                qubit.UpdatePosition();
                buttonReleased = false;
                audioSource.PlayOneShot(audioClipA, 1f) ;
                Debug.Log("A");
                triggered[c] = true;
                Instantiate(gates[c], innerSphere.transform.position, Quaternion.identity);
                gates[c].transform.position = innerSphere.transform.position;
                gates[c].transform.rotation = gatespin[c];
            }
            if (rightHand.FindAction("A").WasReleasedThisFrame())
            {
                int c = 2;
                buttonReleased = true;
                triggered[c] = false;
                //gates[c].SetActive(triggered[c]);
            }
            if (rightHand.FindAction("B").WasPressedThisFrame() && buttonReleased)
            {
                int c = 3;
                ApplyPhaseGate(qubit);
                qubit.UpdatePosition();
                buttonReleased = false;
                audioSource.PlayOneShot(audioClipB, 1f);
                Debug.Log("B");
                triggered[c] = true;
                Instantiate(gates[c], innerSphere.transform.position, Quaternion.identity);
                gates[c].transform.position = innerSphere.transform.position;
                gates[c].transform.rotation = gatespin[c];
            }
            if (rightHand.FindAction("B").WasReleasedThisFrame())
            {
                int c = 3;
                buttonReleased = true;
                triggered[c] = false;
                //gates[c].SetActive(triggered[c]);
            }
        }
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
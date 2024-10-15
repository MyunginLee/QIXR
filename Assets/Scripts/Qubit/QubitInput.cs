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
    [SerializeField] private XRGrabInteractable interactableObject;
    [SerializeField] private float rotationSpeed = 2000.0f;
    private InputAction rotate;
    private InputAction translate;
    private InputActionMap leftHand;
    private InputActionMap rightHand;
    private bool buttonReleased = true;
    private string grabbedObject = "";

    AudioSource audioSource;

    [SerializeField]
    public AudioClip audioClipX, audioClipY, audioClipA, audioClipB;

    void Start()
    {
        interactableObject.selectEntered.AddListener(OnGrab);
        interactableObject.selectExited.AddListener(OnRelease);
        audioSource = GetComponent<AudioSource>();
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

        if (grabbedObject == qubit.name)
        {
            qubit.transform.Rotate(Vector3.up, rotate.ReadValue<Vector2>().x * rotationSpeed * Time.deltaTime);
            qubit.transform.Rotate(Vector3.right, -translate.ReadValue<Vector2>().y * rotationSpeed * Time.deltaTime);
            if (leftHand.FindAction("X").WasReleasedThisFrame() && buttonReleased)
            {
                ApplyPauliX(qubit);
                qubit.UpdatePosition();
                buttonReleased = false;
                audioSource.PlayOneShot(audioClipX, 1f);
            }
            if (leftHand.FindAction("X").WasReleasedThisFrame())
            {
                buttonReleased = true;
            }
            if (leftHand.FindAction("Y").WasPressedThisFrame() && buttonReleased)
            {
                ApplyPauliZ(qubit);
                qubit.UpdatePosition();
                buttonReleased = false;
                audioSource.PlayOneShot(audioClipY, 1f);
            }
            if (leftHand.FindAction("Y").WasReleasedThisFrame())
            {
                buttonReleased = true;
            }
            if (rightHand.FindAction("A").WasPressedThisFrame() && buttonReleased)
            {
                ApplyHadamard(qubit);
                qubit.UpdatePosition();
                buttonReleased = false;
                audioSource.PlayOneShot(audioClipA, 1f) ;
                Debug.Log("A");
            }
            if (rightHand.FindAction("A").WasReleasedThisFrame())
            {
                buttonReleased = true;
            }
            if (rightHand.FindAction("B").WasPressedThisFrame() && buttonReleased)
            {
                ApplyPhaseGate(qubit);
                qubit.UpdatePosition();
                buttonReleased = false;
                audioSource.PlayOneShot(audioClipB, 1f);
                Debug.Log("B");
            }
            if (rightHand.FindAction("B").WasReleasedThisFrame())
            {
                buttonReleased = true;
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
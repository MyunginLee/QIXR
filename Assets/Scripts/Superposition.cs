using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
public class Superposition : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private InputActionAsset measureActions;
    [SerializeField] private Transform qubit;
    [SerializeField] private XRGrabInteractable interactableObject;
    [SerializeField] private float rotationSpeed = 50.0f;
    private InputAction rotate;
    private InputAction translate;


    private float radius;
    private Vector3 currAngle;

    private string grabbedObject = "";


    void Start()
    {

        radius = Vector3.Distance(transform.position, qubit.position);
        Vector3 direction = (transform.position - qubit.position).normalized;
        currAngle = Quaternion.LookRotation(direction).eulerAngles;

        interactableObject.selectEntered.AddListener(OnGrab);
        interactableObject.selectExited.AddListener(OnRelease);
    }

    private void OnEnable()
    {
        var actionMap = inputActions.FindActionMap("XRI LeftHand Interaction");

        rotate = actionMap.FindAction("Rotate Anchor");
        translate = actionMap.FindAction("Translate Anchor");
    }


    private void Update()
    {
        
        if (grabbedObject == qubit.name)
        {
            currAngle.y += rotate.ReadValue<Vector2>().x * rotationSpeed * Time.deltaTime;
            currAngle.x -= translate.ReadValue<Vector2>().y * rotationSpeed * Time.deltaTime;
            transform.position = qubit.position + (Quaternion.Euler(currAngle) * Vector3.forward) * radius;
            transform.LookAt(qubit);
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
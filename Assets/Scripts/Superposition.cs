using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
public class Superposition : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private InputActionAsset measureActions;
    [SerializeField] private Transform qubit;
    [SerializeField] private XRGrabInteractable interactableObject;
    [SerializeField] private float rotationSpeed = 2000.0f;
    private InputAction rotate;
    private InputAction translate;



    private string grabbedObject = "";


    void Start()
    {
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
            qubit.transform.Rotate(Vector3.up, rotate.ReadValue<Vector2>().x * rotationSpeed * Time.deltaTime);
            qubit.transform.Rotate(Vector3.right, -translate.ReadValue<Vector2>().y * rotationSpeed * Time.deltaTime);
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
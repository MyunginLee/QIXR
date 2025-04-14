using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class XRGrabToggle : MonoBehaviour
{
    public bool isActive = true;

    private XRGrabInteractable grabInteractable;
    private InteractionLayerMask defaultLayer;

    void Start()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        // Save the original layer so we can restore it
        defaultLayer = grabInteractable.interactionLayers;
    }

    void Update()
    {
        isActive = !QubitManager.isMeasureActive;
        if (!isActive)
        {
            grabInteractable.interactionLayers = 0; // disables interaction
        }
        else
        {
            grabInteractable.interactionLayers = defaultLayer; // restore interaction
        }
    }
}

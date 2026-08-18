using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.VFX;
using static QubitManager;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class Qubit : MonoBehaviour
{
    public VisualEffect HadamardEffect, PauliXEffect, PauliZEffect, PhaseSEffect;
    public VisualEffectAsset HadamardEffectAsset, PauliXEffectAsset, PauliZEffectAsset, PhaseSEffectAsset;

    public GameObject HadamardGuide, PauliXGuide, PhaseSGuide, PauliZGuide;

    [SerializeField] private GameObject dot;
    [SerializeField] private LineRenderer lineRenderer;

    [FormerlySerializedAs("index")]
    [SerializeField, Min(0)] private int qubitId;
    public int index => qubitId;
    public Transform DotTransform => dot != null ? dot.transform : null;
    private AudioSource audioSource;
    private XRGrabInteractable grabInteractable;

    [SerializeField]
    public AudioClip audioClipH, audioClipX, audioClipZ, audioClipS;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, dot.transform.position);
        }
    }

    private void OnDestroy()
    {
        UnregisterQubitInstance(this);
    }

    private void Update()
    {
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, dot.transform.position);
        }

        UpdatePosition();
        SyncGrabScale();
    }

    public int GetIndex()
    {
        return qubitId;
    }

    public void UpdatePosition()
    {
        EntanglementSnapshot snapshot = GetEntanglementSnapshot();
        if (snapshot == null || dot == null || qubitId >= snapshot.Nodes.Count)
        {
            return;
        }

        dot.transform.localPosition = snapshot.GetNode(qubitId).BlochVector * 0.5f;
        dot.transform.LookAt(transform);
    }

    private void OnTriggerEnter(Collider other)
    {
        Vector3 spawnPos = transform.position + new Vector3(0, 0.2f, 0);

        switch (other.name)
        {
            case "Hadamard":
                ApplyHadamard(this);
                audioSource.PlayOneShot(audioClipH, 1f);
                if (HadamardGuide != null)
                {
                    Instantiate(HadamardGuide, spawnPos, Quaternion.identity, transform);
                }
                break;

            case "Pauli-X":
                ApplyPauliX(this);
                audioSource.PlayOneShot(audioClipX, 1f);
                if (PauliXGuide != null)
                {
                    Instantiate(PauliXGuide, spawnPos, Quaternion.identity, transform);
                }
                break;

            case "Pauli-Z":
                ApplyPauliZ(this);
                audioSource.PlayOneShot(audioClipZ, 1f);
                if (PauliZGuide != null)
                {
                    Instantiate(PauliZGuide, spawnPos, Quaternion.identity, transform);
                }
                break;

            case "Phase-S":
                ApplyPhaseGate(this);
                audioSource.PlayOneShot(audioClipS, 1f);
                if (PhaseSGuide != null)
                {
                    Instantiate(PhaseSGuide, spawnPos, Quaternion.identity, transform);
                }
                break;

            default:
                break;
        }
    }

    private void SyncGrabScale()
    {
        if (grabInteractable != null && grabInteractable.isSelected)
        {
            grabInteractable.SetTargetLocalScale(transform.localScale);
        }
    }
}



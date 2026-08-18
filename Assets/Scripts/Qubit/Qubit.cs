using Complex = System.Numerics.Complex;
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
        ComplexMatrix state = GetDensityMatrix();
        if (state == null || dot == null)
        {
            return;
        }

        if (state.Columns > 2)
        {
            ComplexMatrix reduced = PartialTrace(index);
            Complex p10 = reduced[1, 0];
            Complex p01 = reduced[0, 1];
            Complex p00 = reduced[0, 0];
            Complex p11 = reduced[1, 1];

            Vector3 bloch = new Vector3(
                2f * (float)p01.Real,
                2f * (float)p10.Imaginary,
                (float)(p00.Real - p11.Real)) / 2f;

            dot.transform.localPosition = bloch;
            dot.transform.LookAt(transform);
        }
        else
        {
            ComplexMatrix matrix = state;
            Complex p10 = matrix[1, 0];
            Complex p01 = matrix[0, 1];
            Complex p00 = matrix[0, 0];
            Complex p11 = matrix[1, 1];

            Vector3 bloch = new Vector3(
                2f * (float)p01.Real,
                2f * (float)p10.Imaginary,
                (float)(p00.Real - p11.Real)) / 2f;

            dot.transform.localPosition = bloch;
            dot.transform.LookAt(transform);
        }
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



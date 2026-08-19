using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Serialization;
using UnityEngine.VFX;
using ArtsOfEntanglement.Colocation;
using static QubitManager;
using UnityEngine.XR.Interaction.Toolkit;
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
    private readonly List<UiAnchor> uiAnchors = new List<UiAnchor>();
    private bool uiAnchorCacheDirty = true;
    public bool IsLocallyGrabbed => grabInteractable != null && grabInteractable.isSelected;

    private struct UiAnchor
    {
        public Transform Transform;
        public Vector3 WorldScale;
        public Vector3 OffsetInParentSpace;
    }

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
        RefreshUiAnchors();
    }

    private void OnTransformChildrenChanged()
    {
        // Gate guides and world-space UI can be instantiated while running.
        uiAnchorCacheDirty = true;
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(HandleGrabStarted);
            grabInteractable.selectExited.AddListener(HandleGrabEnded);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(HandleGrabStarted);
            grabInteractable.selectExited.RemoveListener(HandleGrabEnded);
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

        if (IsLocallyGrabbed)
        {
            QuantumSessionState.SubmitLocalPose(qubitId, transform.position, transform.rotation);
        }
    }

    private void LateUpdate()
    {
        if (uiAnchorCacheDirty)
        {
            RefreshUiAnchors();
        }

        for (int i = uiAnchors.Count - 1; i >= 0; i--)
        {
            UiAnchor anchor = uiAnchors[i];
            if (anchor.Transform == null || anchor.Transform.parent == null ||
                !anchor.Transform.IsChildOf(transform))
            {
                uiAnchors.RemoveAt(i);
                continue;
            }

            Transform parent = anchor.Transform.parent;
            Vector3 parentScale = parent.lossyScale;
            if (Mathf.Abs(parentScale.x) < 0.0001f ||
                Mathf.Abs(parentScale.y) < 0.0001f ||
                Mathf.Abs(parentScale.z) < 0.0001f)
            {
                continue;
            }

            // Preserve readable world-space UI while Entanglement changes this
            // root transform's scale. Its position remains attached to Qubit.
            anchor.Transform.localScale = new Vector3(
                anchor.WorldScale.x / parentScale.x,
                anchor.WorldScale.y / parentScale.y,
                anchor.WorldScale.z / parentScale.z);
            anchor.Transform.localPosition = parent.InverseTransformVector(
                parent.rotation * anchor.OffsetInParentSpace);
        }
    }

    private void RefreshUiAnchors()
    {
        uiAnchorCacheDirty = false;
        uiAnchors.Clear();

        Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Transform candidate = canvases[i].transform;
            if (candidate != transform && !HasCanvasAncestor(candidate.parent))
            {
                AddUiAnchor(candidate);
            }
        }

        TMP_Text[] textElements = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < textElements.Length; i++)
        {
            Transform candidate = textElements[i].transform;
            if (candidate != transform && !HasCanvasAncestor(candidate) && !HasUiAnchor(candidate))
            {
                AddUiAnchor(candidate);
            }
        }
    }

    private bool HasCanvasAncestor(Transform candidate)
    {
        for (Transform current = candidate; current != null && current != transform; current = current.parent)
        {
            if (current.GetComponent<Canvas>() != null)
            {
                return true;
            }
        }
        return false;
    }

    private bool HasUiAnchor(Transform candidate)
    {
        for (int i = 0; i < uiAnchors.Count; i++)
        {
            if (candidate.IsChildOf(uiAnchors[i].Transform))
            {
                return true;
            }
        }
        return false;
    }

    private void AddUiAnchor(Transform candidate)
    {
        Transform parent = candidate.parent;
        if (parent == null)
        {
            return;
        }

        uiAnchors.Add(new UiAnchor
        {
            Transform = candidate,
            WorldScale = candidate.lossyScale,
            OffsetInParentSpace = Quaternion.Inverse(parent.rotation) *
                                  (candidate.position - parent.position)
        });
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
        // Remote replicas can also overlap local gate colliders. Only the
        // Fusion-approved grab owner may turn that overlap into a gate event.
        if (QuantumSessionState.IsNetworkSessionActive && !QuantumSessionState.IsLocalGrabApproved(qubitId))
        {
            return;
        }

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

    // Called by the XR Interaction Toolkit events configured on the existing
    // grab interactable. QubitInput also calls these methods as a fallback.
    public void NotifyGrabStarted()
    {
        QuantumSessionState.BeginLocalGrab(qubitId);
    }

    public void NotifyGrabEnded()
    {
        QuantumSessionState.EndLocalGrab(qubitId);
    }

    private void HandleGrabStarted(SelectEnterEventArgs args)
    {
        NotifyGrabStarted();
    }

    private void HandleGrabEnded(SelectExitEventArgs args)
    {
        NotifyGrabEnded();
    }

    private void SyncGrabScale()
    {
        if (grabInteractable != null && grabInteractable.isSelected)
        {
            grabInteractable.SetTargetLocalScale(transform.localScale);
        }
    }
}



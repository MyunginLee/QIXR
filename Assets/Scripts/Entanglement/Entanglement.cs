using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(AudioSource))]
public class Entanglement : MonoBehaviour
{
    private const float NodeBaseScale = 0.3f;
    private const int ArcSegments = 20;
    private static readonly Color[] PairColors =
    {
        new Color(0.10f, 0.85f, 1.00f), // Q0-Q1: cyan
        new Color(1.00f, 0.60f, 0.10f), // Q0-Q2: amber
        new Color(0.95f, 0.25f, 0.90f)  // Q1-Q2: magenta
    };

    [SerializeField, Range(0, 300)] private int numberOfStrings = 96;
    [SerializeField] public float trailtime = 1f;
    [Header("Pair Particle Fields")]
    [SerializeField, Min(0f)] private float gravityStrength = 0.55f;
    [SerializeField, Min(0.01f)] private float gravitySofteningRadius = 0.16f;
    [SerializeField, Min(0.1f)] private float maximumParticleSpeed = 1.15f;
    [SerializeField, Range(1, 8)] private int simulationSubsteps = 3;
    [SerializeField, Min(0f)] private float velocityDamping = 0.7f;
    [SerializeField, Min(0f)] private float particleRepulsion = 0.06f;
    [SerializeField, Min(0.01f)] private float particleRepulsionRadius = 0.24f;
    [SerializeField, Range(0f, 0.1f)] private float particleActivationThreshold = 0.0001f;
    [SerializeField, Range(0f, 0.1f)] private float particleDeactivationThreshold = 0.00005f;
    [SerializeField] public AudioClip SoundUntangle;
    [SerializeField] public AudioClip SoundEntangle;

    public static GameObject[] qubits;
    public static bool[] entangled;

    private Qubit[] orderedQubits;
    private Transform[] shells;
    private BodyProperty[] stringBodies;
    private Transform[] strings;
    private TrailRenderer[] trails;
    private PairVisual[] pairVisuals;
    private GameObject triadMarker;
    private Renderer triadRenderer;
    private TextMeshPro triadLabel;
    private TextMeshPro guideLabel;
    private Material sharedVisualMaterial;
    private AudioSource audioSource;
    private bool wasInteracting;
    private string lastGuideText;
    private MaterialPropertyBlock triadProperties;

    private struct BodyProperty
    {
        public Vector3 velocity;
        public Vector3 acceleration;
        public int pairIndex;
        public float orbitDirection;
        public bool trailWasActive;
        public bool hasTrailSample;
        public Vector3 lastTrailSample;
    }

    private sealed class PairVisual
    {
        public int First;
        public int Second;
        public LineRenderer Arc;
        public TextMeshPro Label;
        public double LastDisplayedMetric = -1.0;
        public float TargetStrength;
        public float VisualStrength;
        public float LastTrailStrength = -1f;
        public bool ParticleFieldActive;
        public bool HasSmoothedAxis;
        public Vector3 SmoothedAxis;
    }

    private void Start()
    {
        orderedQubits = FindObjectsByType<Qubit>(FindObjectsSortMode.None);
        Array.Sort(orderedQubits, (left, right) => left.GetIndex().CompareTo(right.GetIndex()));
        qubits = new GameObject[orderedQubits.Length];
        shells = new Transform[orderedQubits.Length];
        entangled = new bool[orderedQubits.Length];
        for (int i = 0; i < orderedQubits.Length; i++)
        {
            qubits[i] = orderedQubits[i].gameObject;
            GameObject shell = GameObject.Find($"QubitShell {i + 1}");
            shells[i] = shell != null ? shell.transform : null;
        }

        audioSource = GetComponent<AudioSource>();
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        sharedVisualMaterial = new Material(shader) { name = "QIXR Phase 3 Visual Material" };
        triadProperties = new MaterialPropertyBlock();
        CreatePairVisuals();
        CreateTriadVisual();
        CreateGuideLabel();
        CreateStringPool();
        UpdateNodeAndPairVisuals();
    }

    private void Update()
    {
        if (orderedQubits == null || orderedQubits.Length == 0)
        {
            return;
        }

        UpdateNodeAndPairVisuals();
        UpdateStrings();
        FaceLabelsToViewer();
    }

    private void OnValidate()
    {
        particleActivationThreshold = Mathf.Max(0f, particleActivationThreshold);
        particleDeactivationThreshold = Mathf.Clamp(
            particleDeactivationThreshold, 0f, particleActivationThreshold);
        gravitySofteningRadius = Mathf.Max(0.01f, gravitySofteningRadius);
        simulationSubsteps = Mathf.Clamp(simulationSubsteps, 1, 8);
    }

    private void OnDestroy()
    {
        if (sharedVisualMaterial != null)
        {
            Destroy(sharedVisualMaterial);
        }
    }

    public float ComputeQubitScale(Qubit qubit)
    {
        EntanglementSnapshot snapshot = QubitManager.GetEntanglementSnapshot();
        if (qubit == null || snapshot == null)
        {
            return 1f;
        }
        return snapshot.GetNode(qubit.GetIndex()).BlochRadius;
    }

    private void UpdateNodeAndPairVisuals()
    {
        EntanglementSnapshot snapshot = QubitManager.GetEntanglementSnapshot();
        if (snapshot == null || snapshot.Nodes.Count != orderedQubits.Length)
        {
            return;
        }

        Array.Clear(entangled, 0, entangled.Length);
        int activeInteractionCount = 0;
        int entangledPairCount = 0;
        for (int pairIndex = 0; pairIndex < pairVisuals.Length; pairIndex++)
        {
            PairVisual pair = pairVisuals[pairIndex];
            Vector3 first = orderedQubits[pair.First].transform.position;
            Vector3 second = orderedQubits[pair.Second].transform.position;
            float distance = Vector3.Distance(first, second);
            if (distance <= QubitManager.THRESHOLD_DISTANCE)
            {
                activeInteractionCount++;
            }
            PairEntanglementMetric pairMetric = snapshot.GetPair(pair.First, pair.Second);
            UpdatePairArc(pair, first, second, pairMetric, pairIndex);
            if (pair.ParticleFieldActive)
            {
                entangledPairCount++;
            }
        }

        for (int i = 0; i < orderedQubits.Length; i++)
        {
            QubitMetric nodeMetric = snapshot.GetNode(i);
            entangled[i] = nodeMetric.Renyi2Entropy > EntanglementMetrics.CorrelationEntropyThreshold;
            // Keep a small grab target even when the Bloch radius reaches zero.
            float scale = Mathf.Lerp(0.06f, NodeBaseScale, nodeMetric.BlochRadius);
            orderedQubits[i].transform.localScale = Vector3.one * scale;
            if (shells[i] != null)
            {
                shells[i].position = orderedQubits[i].transform.position;
            }
        }

        UpdateTriad(snapshot.HasThreePartyCorrelation,
            (float)snapshot.ThreePartyCorrelationStrength);
        UpdateGuide(activeInteractionCount, snapshot.HasThreePartyCorrelation);
        UpdateInteractionAudio(entangledPairCount > 0 || snapshot.HasThreePartyCorrelation);

    }

    private void CreatePairVisuals()
    {
        var visuals = new List<PairVisual>();
        for (int first = 0; first < orderedQubits.Length; first++)
        {
            for (int second = first + 1; second < orderedQubits.Length; second++)
            {
                int pairIndex = visuals.Count;
                var arcObject = new GameObject($"Pair Arc Q{first}-Q{second}");
                arcObject.transform.SetParent(transform, false);
                LineRenderer arc = arcObject.AddComponent<LineRenderer>();
                arc.useWorldSpace = true;
                arc.positionCount = ArcSegments;
                arc.widthMultiplier = 0.012f;
                arc.numCapVertices = 3;
                arc.numCornerVertices = 2;
                arc.material = sharedVisualMaterial;
                arc.startColor = PairColors[pairIndex % PairColors.Length];
                arc.endColor = arc.startColor;

                TextMeshPro label = CreateWorldLabel(
                    arcObject.transform, $"Q{first}–Q{second} entanglement", 0.75f);
                visuals.Add(new PairVisual { First = first, Second = second, Arc = arc, Label = label });
            }
        }
        pairVisuals = visuals.ToArray();
    }

    private void UpdatePairArc(
        PairVisual pair, Vector3 first, Vector3 second,
        PairEntanglementMetric metric, int pairIndex)
    {
        pair.TargetStrength = Mathf.Clamp01((float)metric.LogarithmicNegativity);
        float response = 1f - Mathf.Exp(-7f * Time.deltaTime);
        pair.VisualStrength = Mathf.Lerp(pair.VisualStrength, pair.TargetStrength, response);
        if (pair.ParticleFieldActive)
        {
            pair.ParticleFieldActive = pair.TargetStrength > particleDeactivationThreshold;
        }
        else
        {
            pair.ParticleFieldActive = pair.TargetStrength >= particleActivationThreshold;
        }

        Vector3 desiredAxis = second - first;
        if (desiredAxis.sqrMagnitude > 0.0001f)
        {
            desiredAxis.Normalize();
            if (!pair.HasSmoothedAxis)
            {
                pair.SmoothedAxis = desiredAxis;
                pair.HasSmoothedAxis = true;
            }
            else
            {
                pair.SmoothedAxis = Vector3.Slerp(pair.SmoothedAxis, desiredAxis, response).normalized;
            }
        }

        pair.Arc.enabled = pair.ParticleFieldActive;
        pair.Label.gameObject.SetActive(pair.ParticleFieldActive);
        if (!pair.ParticleFieldActive)
        {
            return;
        }

        float intensity = Mathf.Clamp01((float)metric.LogarithmicNegativity);
        float curvature = 0.12f + pairIndex * 0.08f + intensity * 0.18f;
        Vector3 midpoint = (first + second) * 0.5f + Vector3.up * curvature;
        for (int segment = 0; segment < ArcSegments; segment++)
        {
            float t = segment / (float)(ArcSegments - 1);
            Vector3 position = (1f - t) * (1f - t) * first +
                               2f * (1f - t) * t * midpoint + t * t * second;
            pair.Arc.SetPosition(segment, position);
        }

        Color color = PairColors[pairIndex % PairColors.Length];
        color.a = 0.45f + 0.55f * intensity;
        pair.Arc.startColor = color;
        pair.Arc.endColor = color;
        pair.Arc.widthMultiplier = 0.008f + 0.018f * intensity;
        pair.Label.color = color;
        pair.Label.transform.position = midpoint + Vector3.up * 0.06f;
        if (Math.Abs(metric.LogarithmicNegativity - pair.LastDisplayedMetric) > 0.01)
        {
            pair.Label.text = $"Q{pair.First}–Q{pair.Second}  E_N={metric.LogarithmicNegativity:F2}";
            pair.LastDisplayedMetric = metric.LogarithmicNegativity;
        }
    }

    private void CreateTriadVisual()
    {
        triadMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        triadMarker.name = "Three-Party Correlation Marker";
        triadMarker.transform.SetParent(transform, false);
        Collider markerCollider = triadMarker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Destroy(markerCollider);
        }
        triadRenderer = triadMarker.GetComponent<Renderer>();
        triadRenderer.sharedMaterial = sharedVisualMaterial;
        triadLabel = CreateWorldLabel(triadMarker.transform, "◆ three-party correlation", 0.85f);
        triadLabel.transform.localPosition = Vector3.up * 0.18f;
        triadMarker.SetActive(false);
    }

    private void UpdateTriad(bool visible, float meanEntropy)
    {
        triadMarker.SetActive(visible);
        if (!visible)
        {
            return;
        }

        Vector3 center = Vector3.zero;
        for (int i = 0; i < orderedQubits.Length; i++)
        {
            center += orderedQubits[i].transform.position;
        }
        center /= orderedQubits.Length;
        triadMarker.transform.position = center + Vector3.up * 0.28f;
        triadMarker.transform.localScale = Vector3.one * Mathf.Lerp(0.07f, 0.16f, Mathf.Clamp01(meanEntropy));
        Color color = Color.Lerp(new Color(1f, 0.85f, 0.2f), Color.white, Mathf.Clamp01(meanEntropy));
        triadProperties.SetColor("_Color", color);
        triadRenderer.SetPropertyBlock(triadProperties);
        triadLabel.color = color;
    }

    private void CreateGuideLabel()
    {
        guideLabel = CreateWorldLabel(transform, string.Empty, 0.9f);
        guideLabel.name = "Three-Qubit Guided Task";
        guideLabel.alignment = TextAlignmentOptions.Center;
    }

    private void UpdateGuide(int activePairCount, bool threePartyCorrelation)
    {
        Vector3 center = Vector3.zero;
        for (int i = 0; i < orderedQubits.Length; i++)
        {
            center += orderedQubits[i].transform.position;
        }
        center /= orderedQubits.Length;
        guideLabel.transform.position = center + Vector3.up * 0.8f;

        string message;
        if (orderedQubits.Length != 3)
        {
            message = $"{orderedQubits.Length}-qubit compatibility mode";
        }
        else if (activePairCount == 0)
        {
            message = "Step 1  Hold Grip to grab Q0\nMove it near Q1, then release";
        }
        else if (activePairCount == 1)
        {
            message = "Step 2  Hold Grip to grab Q2\nMove it near Q0 or Q1, then release";
        }
        else if (!threePartyCorrelation)
        {
            message = "Step 3  Release the grabbed qubit and observe\nThe ◆ marker denotes three-party correlation";
        }
        else
        {
            message = "◆ Three-party correlation\nPair arcs show logarithmic negativity E_N";
        }

        if (!string.Equals(message, lastGuideText, StringComparison.Ordinal))
        {
            guideLabel.text = message;
            lastGuideText = message;
        }
    }

    private void CreateStringPool()
    {
        numberOfStrings = Mathf.Max(0, numberOfStrings);
        stringBodies = new BodyProperty[numberOfStrings];
        strings = new Transform[numberOfStrings];
        trails = new TrailRenderer[numberOfStrings];
        int pairCount = pairVisuals.Length;

        for (int i = 0; i < numberOfStrings; i++)
        {
            var stringObject = new GameObject($"Correlation Trail {i:000}");
            stringObject.transform.SetParent(transform, false);
            strings[i] = stringObject.transform;
            stringBodies[i].pairIndex = pairCount > 0 ? i % pairCount : -1;
            stringBodies[i].orbitDirection = ((i / Mathf.Max(1, pairCount)) & 1) == 0 ? 1f : -1f;

            TrailRenderer trail = stringObject.AddComponent<TrailRenderer>();
            trail.time = trailtime;
            trail.startWidth = 0.008f;
            trail.endWidth = 0.002f;
            trail.minVertexDistance = 0.0025f;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 3;
            trail.material = sharedVisualMaterial;
            // Positions are also added explicitly at each physics substep. The
            // automatic emission remains enabled while active so Unity builds
            // the trail mesh on every supported renderer path.
            trail.emitting = false;
            trails[i] = trail;

            PlaceStringInPairField(i);
        }

        for (int pairIndex = 0; pairIndex < pairCount; pairIndex++)
        {
            UpdatePairTrailAppearance(pairIndex, 0f);
        }
    }

    private void UpdateStrings()
    {
        if (pairVisuals.Length == 0)
        {
            return;
        }

        for (int pairIndex = 0; pairIndex < pairVisuals.Length; pairIndex++)
        {
            PairVisual pair = pairVisuals[pairIndex];
            if (Mathf.Abs(pair.VisualStrength - pair.LastTrailStrength) > 0.015f ||
                pair.LastTrailStrength < 0f)
            {
                UpdatePairTrailAppearance(pairIndex, pair.VisualStrength);
                pair.LastTrailStrength = pair.VisualStrength;
            }
        }

        float frameTime = Mathf.Min(Time.deltaTime, 0.05f);
        int substeps = Mathf.Clamp(simulationSubsteps, 1, 8);
        float stepTime = frameTime / substeps;
        for (int stringIndex = 0; stringIndex < strings.Length; stringIndex++)
        {
            BodyProperty body = stringBodies[stringIndex];
            if (body.pairIndex < 0 || body.pairIndex >= pairVisuals.Length)
            {
                continue;
            }

            PairVisual pair = pairVisuals[body.pairIndex];
            TrailRenderer trail = trails[stringIndex];
            bool active = pair.ParticleFieldActive;
            trail.emitting = active;
            if (active && !body.trailWasActive)
            {
                trail.Clear();
                body.lastTrailSample = strings[stringIndex].position;
                body.hasTrailSample = true;
                trail.AddPosition(body.lastTrailSample);
            }
            else if (!active)
            {
                body.hasTrailSample = false;
            }
            body.trailWasActive = active;

            Vector3 first = orderedQubits[pair.First].transform.position;
            Vector3 second = orderedQubits[pair.Second].transform.position;
            Vector3 midpoint = (first + second) * 0.5f;
            Vector3 pairAxis = pair.HasSmoothedAxis ? pair.SmoothedAxis : Vector3.right;
            Vector3 position = strings[stringIndex].position;
            float minimumSampleDistanceSquared = trail.minVertexDistance * trail.minVertexDistance;

            for (int step = 0; step < substeps; step++)
            {
                float strength = pair.VisualStrength;
                Vector3 acceleration = SoftenedGravity(position, first, strength) +
                                       SoftenedGravity(position, second, strength);
                acceleration += PairParticleRepulsion(position, stringIndex, body.pairIndex, strength);

                // A weak center force confines each field to its own pair. The
                // tangential component prevents trails collapsing into straight
                // radial lines while remaining continuous as the qubits move.
                Vector3 fromCenter = position - midpoint;
                acceleration -= fromCenter * Mathf.Lerp(0.35f, 0.7f, strength);
                Vector3 tangential = Vector3.Cross(pairAxis, fromCenter);
                if (tangential.sqrMagnitude > 0.0001f)
                {
                    acceleration += tangential.normalized *
                                    (0.22f * strength * body.orbitDirection);
                }

                body.acceleration = Vector3.ClampMagnitude(acceleration, 3f);
                body.velocity += body.acceleration * stepTime;
                body.velocity *= Mathf.Exp(-velocityDamping * stepTime);
                float speedLimit = Mathf.Lerp(0.4f, maximumParticleSpeed, strength);
                body.velocity = Vector3.ClampMagnitude(body.velocity, speedLimit);
                position += body.velocity * stepTime;

                if (active && (!body.hasTrailSample ||
                    (position - body.lastTrailSample).sqrMagnitude >= minimumSampleDistanceSquared))
                {
                    trail.AddPosition(position);
                    body.lastTrailSample = position;
                    body.hasTrailSample = true;
                }
            }

            strings[stringIndex].position = position;
            stringBodies[stringIndex] = body;
        }
    }

    private Vector3 SoftenedGravity(Vector3 position, Vector3 attractor, float strength)
    {
        Vector3 offset = attractor - position;
        float softenedSquaredDistance = offset.sqrMagnitude +
                                        gravitySofteningRadius * gravitySofteningRadius;
        float inverseDistance = 1f / Mathf.Sqrt(softenedSquaredDistance);
        float inverseDistanceCubed = inverseDistance * inverseDistance * inverseDistance;
        return offset * (gravityStrength * strength * inverseDistanceCubed);
    }

    private Vector3 PairParticleRepulsion(
        Vector3 position, int stringIndex, int pairIndex, float strength)
    {
        float radiusSquared = particleRepulsionRadius * particleRepulsionRadius;
        Vector3 acceleration = Vector3.zero;
        for (int otherIndex = 0; otherIndex < strings.Length; otherIndex++)
        {
            if (otherIndex == stringIndex || stringBodies[otherIndex].pairIndex != pairIndex)
            {
                continue;
            }

            Vector3 separation = position - strings[otherIndex].position;
            float distanceSquared = separation.sqrMagnitude;
            if (distanceSquared < 0.000001f || distanceSquared >= radiusSquared)
            {
                continue;
            }

            float falloff = 1f - distanceSquared / radiusSquared;
            acceleration += separation.normalized *
                            (particleRepulsion * falloff * Mathf.Lerp(0.35f, 1f, strength) /
                             (distanceSquared + 0.01f));
        }
        return acceleration;
    }

    private void PlaceStringInPairField(int stringIndex)
    {
        BodyProperty body = stringBodies[stringIndex];
        if (body.pairIndex < 0 || body.pairIndex >= pairVisuals.Length)
        {
            strings[stringIndex].position = transform.position;
            return;
        }

        PairVisual pair = pairVisuals[body.pairIndex];
        Vector3 first = orderedQubits[pair.First].transform.position;
        Vector3 second = orderedQubits[pair.Second].transform.position;
        Vector3 axis = second - first;
        if (axis.sqrMagnitude < 0.0001f)
        {
            axis = pair.HasSmoothedAxis ? pair.SmoothedAxis : Vector3.right;
        }
        axis.Normalize();
        if (!pair.HasSmoothedAxis)
        {
            pair.SmoothedAxis = axis;
            pair.HasSmoothedAxis = true;
        }

        Vector3 normal = Vector3.Cross(axis, Vector3.up);
        if (normal.sqrMagnitude < 0.0001f)
        {
            normal = Vector3.Cross(axis, Vector3.right);
        }
        normal.Normalize();
        Vector3 binormal = Vector3.Cross(axis, normal).normalized;
        int particlesInPair = Mathf.Max(1,
            (numberOfStrings + pairVisuals.Length - 1) / pairVisuals.Length);
        int particleInPair = stringIndex / pairVisuals.Length;
        // Golden-ratio spacing avoids the perfectly symmetric lanes that made
        // particles collapse visually into a few repeated trails.
        float phase = 2f * Mathf.PI * Mathf.Repeat(particleInPair * 0.61803398875f, 1f);
        float radius = 0.11f + 0.09f * Mathf.Repeat(particleInPair * 0.38196601125f, 1f);
        Vector3 radial = (normal * Mathf.Cos(phase) + binormal * Mathf.Sin(phase)) * radius;

        strings[stringIndex].position = (first + second) * 0.5f + radial;
        body.velocity = Vector3.Cross(axis, radial).normalized *
                        (0.12f * body.orbitDirection);
        stringBodies[stringIndex] = body;
        trails[stringIndex].Clear();
    }

    private void UpdatePairTrailAppearance(int pairIndex, float strength)
    {
        Color pairColor = PairColors[pairIndex % PairColors.Length];
        Color brightColor = Color.Lerp(pairColor * 0.65f, pairColor, Mathf.Clamp01(strength));
        brightColor.a = Mathf.Lerp(0.18f, 0.65f, strength);
        Color tailColor = pairColor;
        tailColor.a = 0f;

        for (int i = 0; i < trails.Length; i++)
        {
            if (stringBodies[i].pairIndex != pairIndex)
            {
                continue;
            }

            // startColor/endColor updates the renderer's existing gradient and
            // avoids allocating Gradient/key arrays during strength transitions.
            trails[i].startColor = brightColor;
            trails[i].endColor = tailColor;
            trails[i].startWidth = Mathf.Lerp(0.0035f, 0.01f, strength);
            trails[i].endWidth = 0.001f;
        }
    }

    private void UpdateInteractionAudio(bool interacting)
    {
        if (interacting == wasInteracting)
        {
            return;
        }

        AudioClip clip = interacting ? SoundEntangle : SoundUntangle;
        if (clip != null)
        {
            audioSource.pitch = Random.Range(0.85f, 1.2f);
            audioSource.PlayOneShot(clip, interacting ? 1f : 0.5f);
        }
        wasInteracting = interacting;
    }

    private TextMeshPro CreateWorldLabel(Transform parent, string text, float fontSize)
    {
        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent, false);
        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private void FaceLabelsToViewer()
    {
        Camera viewer = Camera.main;
        if (viewer == null)
        {
            return;
        }

        guideLabel.transform.rotation = FaceCamera(guideLabel.transform, viewer);
        if (triadLabel != null && triadLabel.gameObject.activeInHierarchy)
        {
            triadLabel.transform.rotation = FaceCamera(triadLabel.transform, viewer);
        }
        for (int i = 0; i < pairVisuals.Length; i++)
        {
            if (pairVisuals[i].Label.gameObject.activeInHierarchy)
            {
                pairVisuals[i].Label.transform.rotation = FaceCamera(pairVisuals[i].Label.transform, viewer);
            }
        }
    }

    private static Quaternion FaceCamera(Transform label, Camera viewer)
    {
        Vector3 direction = viewer.transform.position - label.position;
        return direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : label.rotation;
    }
}

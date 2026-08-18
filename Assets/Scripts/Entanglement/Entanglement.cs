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
    private float lastTrailEntropy = -1f;
    private string lastGuideText;
    private MaterialPropertyBlock triadProperties;

    private struct BodyProperty
    {
        public Vector3 velocity;
        public Vector3 acceleration;
    }

    private sealed class PairVisual
    {
        public int First;
        public int Second;
        public LineRenderer Arc;
        public TextMeshPro Label;
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

    private void OnDestroy()
    {
        if (sharedVisualMaterial != null)
        {
            Destroy(sharedVisualMaterial);
        }
    }

    public float ComputeQubitScale(Qubit qubit)
    {
        if (qubit == null || QubitManager.GetDensityMatrix() == null)
        {
            return 1f;
        }

        ComplexMatrix reduced = QubitManager.PartialTrace(qubit.GetIndex());
        return ComputeBlochRadius(reduced);
    }

    private static float ComputeBlochRadius(ComplexMatrix reduced)
    {
        double x = 2.0 * reduced[0, 1].Real;
        double y = -2.0 * reduced[0, 1].Imaginary;
        double z = reduced[0, 0].Real - reduced[1, 1].Real;
        return Mathf.Clamp01((float)Math.Sqrt(x * x + y * y + z * z));
    }

    private void UpdateNodeAndPairVisuals()
    {
        Array.Clear(entangled, 0, entangled.Length);
        int activePairCount = 0;
        for (int pairIndex = 0; pairIndex < pairVisuals.Length; pairIndex++)
        {
            PairVisual pair = pairVisuals[pairIndex];
            Vector3 first = orderedQubits[pair.First].transform.position;
            Vector3 second = orderedQubits[pair.Second].transform.position;
            float distance = Vector3.Distance(first, second);
            bool active = distance <= QubitManager.THRESHOLD_DISTANCE;
            if (active)
            {
                entangled[pair.First] = true;
                entangled[pair.Second] = true;
                activePairCount++;
            }
            UpdatePairArc(pair, first, second, distance, active, pairIndex);
        }

        float entropySum = 0f;
        int correlatedNodes = 0;
        for (int i = 0; i < orderedQubits.Length; i++)
        {
            ComplexMatrix reduced = QubitManager.PartialTrace(i);
            // Keep a small grab target even when the Bloch radius reaches zero.
            float scale = Mathf.Lerp(0.06f, NodeBaseScale, ComputeBlochRadius(reduced));
            orderedQubits[i].transform.localScale = Vector3.one * scale;
            if (shells[i] != null)
            {
                shells[i].position = orderedQubits[i].transform.position;
            }

            double purity = (reduced * reduced).Trace().Real;
            float nodeEntropy = (float)-Math.Log(Math.Max(1e-8, Math.Min(1.0, purity)));
            entropySum += nodeEntropy;
            if (nodeEntropy > 0.05f)
            {
                correlatedNodes++;
            }
        }

        bool threePartyCorrelation = orderedQubits.Length == 3 && correlatedNodes == 3;
        UpdateTriad(threePartyCorrelation, entropySum / orderedQubits.Length);
        UpdateGuide(activePairCount, threePartyCorrelation);
        UpdateInteractionAudio(activePairCount > 0);

        float meanEntropy = entropySum / orderedQubits.Length;
        if (Mathf.Abs(meanEntropy - lastTrailEntropy) > 0.02f)
        {
            UpdateTrailAppearance(meanEntropy);
            lastTrailEntropy = meanEntropy;
        }
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
                    arcObject.transform, $"Q{first}–Q{second} interaction", 0.75f);
                visuals.Add(new PairVisual { First = first, Second = second, Arc = arc, Label = label });
            }
        }
        pairVisuals = visuals.ToArray();
    }

    private void UpdatePairArc(
        PairVisual pair, Vector3 first, Vector3 second, float distance, bool active, int pairIndex)
    {
        pair.Arc.enabled = active;
        pair.Label.gameObject.SetActive(active);
        if (!active)
        {
            return;
        }

        float proximity = 1f - Mathf.Clamp01(distance / QubitManager.THRESHOLD_DISTANCE);
        float coupling = Mathf.Clamp01((float)Math.Abs(
            QubitManager.GetPairCoupling(pair.First, pair.Second)));
        float intensity = Mathf.Max(proximity, coupling);
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
            message = "◆ Three-party correlation\nPair arcs show interactions, not proof of entanglement";
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
        Vector3 center = orderedQubits.Length > 0 ? orderedQubits[0].transform.position : Vector3.zero;

        for (int i = 0; i < numberOfStrings; i++)
        {
            var stringObject = new GameObject($"Correlation Trail {i:000}");
            stringObject.transform.SetParent(transform, false);
            float angle = Mathf.PI * 2f * i / Mathf.Max(1, numberOfStrings);
            stringObject.transform.position = center + new Vector3(
                2.5f * Mathf.Cos(angle), Random.Range(-1.5f, 1.5f), 2.5f * Mathf.Sin(angle));
            strings[i] = stringObject.transform;
            stringBodies[i].velocity = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 0.1f;

            TrailRenderer trail = stringObject.AddComponent<TrailRenderer>();
            trail.time = trailtime;
            trail.startWidth = 0.008f;
            trail.endWidth = 0.002f;
            trail.material = sharedVisualMaterial;
            trail.emitting = false;
            trails[i] = trail;
        }
        UpdateTrailAppearance(0f);
    }

    private void UpdateStrings()
    {
        bool anyInteraction = false;
        for (int i = 0; i < entangled.Length; i++)
        {
            anyInteraction |= entangled[i];
        }

        for (int stringIndex = 0; stringIndex < strings.Length; stringIndex++)
        {
            TrailRenderer trail = trails[stringIndex];
            trail.emitting = anyInteraction;
            if (!anyInteraction)
            {
                stringBodies[stringIndex].velocity *= 0.97f;
                continue;
            }

            Vector3 acceleration = Vector3.zero;
            for (int node = 0; node < orderedQubits.Length; node++)
            {
                if (!entangled[node])
                {
                    continue;
                }
                Vector3 offset = orderedQubits[node].transform.position - strings[stringIndex].position;
                float squaredDistance = Mathf.Max(0.04f, offset.sqrMagnitude);
                acceleration += offset.normalized * (0.45f / squaredDistance);
            }

            acceleration = Vector3.ClampMagnitude(acceleration, 3f);
            stringBodies[stringIndex].acceleration = acceleration;
            stringBodies[stringIndex].velocity = Vector3.ClampMagnitude(
                stringBodies[stringIndex].velocity + acceleration * Time.deltaTime, 1.2f);
            strings[stringIndex].position += stringBodies[stringIndex].velocity * Time.deltaTime;
        }
    }

    private void UpdateTrailAppearance(float meanEntropy)
    {
        Color start = Color.Lerp(new Color(0.15f, 0.75f, 1f), new Color(1f, 0.3f, 0.9f),
            Mathf.Clamp01(meanEntropy));
        Color end = new Color(1f, 0.75f, 0.2f, 0.05f);
        for (int i = 0; i < trails.Length; i++)
        {
            float hueOffset = i / (float)Mathf.Max(1, trails.Length);
            Color varied = Color.Lerp(start, Color.HSVToRGB(hueOffset, 0.55f, 1f), 0.25f);
            trails[i].colorGradient = new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(varied, 0f),
                    new GradientColorKey(end, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0.35f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            };
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

using UnityEngine;

/// <summary>
/// Local, monophonic arpeggio sonification. Relative pair phase chooses the
/// harmonic root and contour; pairwise logarithmic negativity chooses the
/// chord vocabulary. Only one note is ever audible at a time.
/// </summary>
public sealed class QuantumArpeggioComposer : MonoBehaviour
{
    private static readonly int[] CircleOfFifths = { 0, 7, 2, 9, 4, 11, 6, 1, 8, 3, 10, 5 };

    [Header("Arpeggio")]
    [SerializeField, Range(36f, 120f)] private float tempoBpm = 66f;
    [SerializeField, Range(0.001f, 0.25f)] private float entryThreshold = 0.01f;
    [SerializeField, Range(0f, 0.5f)] private float noteGain = 0.12f;
    [SerializeField, Range(0.15f, 0.8f)] private float minimumStepSeconds = 0.26f;
    [SerializeField, Range(0.2f, 1f)] private float maximumStepBeats = 0.72f;

    private readonly int[] openFifth = { 0, 7, 12 };
    private readonly int[] majorTriad = { 0, 4, 7, 12 };
    private readonly int[] minorTriad = { 0, 3, 7, 12 };
    private readonly int[] majorIridescent = { 0, 4, 7, 11, 14 };
    private readonly int[] minorIridescent = { 0, 3, 7, 10, 14 };
    private readonly AudioClip[,] noteBank = new AudioClip[12, 3];

    private AudioSource instrument;
    private float nextStepTime;
    private float lastPhaseDegrees;
    private float phaseVelocity;
    private int arpeggioIndex;
    private int direction = 1;
    private int heldRoot;
    private int heldPairIndex = -1;
    private int cadenceStepsRemaining;
    private int measurementParity;

    private void Awake()
    {
        QubitAudio.SuppressLegacyFm = true;
    }

    private void OnEnable()
    {
        QubitManager.MeasurementCompleted += OnMeasurementCompleted;
    }

    private void Start()
    {
        StopLegacyFmSources();
        int sampleRate = Mathf.Max(22050, AudioSettings.outputSampleRate);
        GameObject sourceObject = new GameObject("Quantum Arpeggio Instrument");
        sourceObject.transform.SetParent(transform, false);
        instrument = sourceObject.AddComponent<AudioSource>();
        instrument.playOnAwake = false;
        instrument.spatialBlend = 0f;
        instrument.priority = 80;

        for (int semitone = 0; semitone < 12; semitone++)
        {
            for (int register = 0; register < 3; register++)
            {
                noteBank[semitone, register] = CreatePluckClip(
                    $"Arpeggio {semitone} {register}", Frequency(semitone, register), sampleRate);
            }
        }
        nextStepTime = Time.time + BeatDuration;
    }

    private void Update()
    {
        if (instrument == null || Time.time < nextStepTime)
        {
            return;
        }

        EntanglementSnapshot snapshot = QubitManager.GetEntanglementSnapshot();
        if (snapshot == null || snapshot.Pairs.Count == 0)
        {
            nextStepTime = Time.time + BeatDuration;
            return;
        }

        if (cadenceStepsRemaining > 0)
        {
            PlayCadenceStep();
            cadenceStepsRemaining--;
            nextStepTime = Time.time + BeatDuration * 0.55f;
            return;
        }

        int pairIndex = FindDominantPair(snapshot, out PairEntanglementMetric pair, out float strength);
        if (pairIndex < 0 || strength < entryThreshold)
        {
            // Silence is the musical representation of a separable configuration.
            nextStepTime = Time.time + BeatDuration;
            arpeggioIndex = 0;
            heldPairIndex = -1;
            return;
        }

        float phase = RelativePhaseDegrees(snapshot.GetNode(pair.FirstQubitId).BlochVector,
            snapshot.GetNode(pair.SecondQubitId).BlochVector);
        UpdateContour(phase);
        int root = RootFromPhase(phase);
        int[] chord = SelectChord(strength, phase, snapshot.HasThreePartyCorrelation);
        if (pairIndex != heldPairIndex || arpeggioIndex == 0)
        {
            // Commit a new root only at a pattern boundary, never mid-arpeggio.
            heldRoot = root;
            heldPairIndex = pairIndex;
        }

        int chordIndex = PatternIndex(chord.Length);
        int register = strength > 0.62f ? 1 : 0;
        if (snapshot.HasThreePartyCorrelation && snapshot.ThreePartyCorrelationStrength > 0.35)
        {
            register = 1;
        }
        PlaySemitone(heldRoot + chord[chordIndex], register,
            noteGain * Mathf.Lerp(0.55f, 1f, strength));

        arpeggioIndex = (arpeggioIndex + 1) % chord.Length;
        float mutualInformation = Mathf.Clamp01((float)pair.MutualInformationBits * 0.5f);
        float stepBeats = Mathf.Lerp(maximumStepBeats, 0.34f, mutualInformation);
        nextStepTime = Time.time + Mathf.Max(minimumStepSeconds, BeatDuration * stepBeats);
    }

    private int FindDominantPair(
        EntanglementSnapshot snapshot, out PairEntanglementMetric dominant, out float strength)
    {
        dominant = null;
        strength = 0f;
        int index = -1;
        for (int i = 0; i < snapshot.Pairs.Count; i++)
        {
            PairEntanglementMetric pair = snapshot.Pairs[i];
            float candidate = Mathf.Clamp01((float)pair.LogarithmicNegativity);
            if (candidate > strength)
            {
                strength = candidate;
                dominant = pair;
                index = i;
            }
        }
        return index;
    }

    private int[] SelectChord(float strength, float phaseDegrees, bool hasThreePartyCorrelation)
    {
        if (strength < 0.18f)
        {
            return openFifth;
        }

        bool minor = Mathf.Cos(phaseDegrees * Mathf.Deg2Rad) < 0f;
        if (strength < 0.58f && !hasThreePartyCorrelation)
        {
            return minor ? minorTriad : majorTriad;
        }
        return minor ? minorIridescent : majorIridescent;
    }

    private void UpdateContour(float currentPhaseDegrees)
    {
        float delta = Mathf.DeltaAngle(lastPhaseDegrees, currentPhaseDegrees);
        phaseVelocity = Mathf.Lerp(phaseVelocity, delta, 0.2f);
        lastPhaseDegrees = currentPhaseDegrees;
        if (Mathf.Abs(phaseVelocity) > 0.08f)
        {
            direction = phaseVelocity > 0f ? 1 : -1;
        }
    }

    private int PatternIndex(int chordLength)
    {
        if (Mathf.Abs(phaseVelocity) <= 0.08f)
        {
            // A stationary relative phase breathes outward and inward.
            int span = Mathf.Max(1, chordLength * 2 - 2);
            int index = arpeggioIndex % span;
            return index < chordLength ? index : span - index;
        }
        return direction > 0 ? arpeggioIndex : chordLength - 1 - arpeggioIndex;
    }

    private void PlayCadenceStep()
    {
        // A measurement resolves the live arpeggio into a short, monophonic
        // cadence. Parity selects bright (even) or inward (odd) third.
        bool odd = (measurementParity & 1) != 0;
        int[] notes = odd ? new[] { 7, 3, 0 } : new[] { 7, 4, 0 };
        int phase = 3 - cadenceStepsRemaining;
        PlaySemitone(heldRoot + notes[Mathf.Clamp(phase, 0, notes.Length - 1)], phase == 0 ? 1 : 0, cadenceStepsRemaining == 1 ? noteGain * 1.2f : noteGain * 0.8f);
    }

    private void PlaySemitone(int semitone, int register, float gain)
    {
        // Assigning clip + Play stops the previous note, guaranteeing a single
        // audible pitch even during rapid, high-information arpeggios.
        instrument.Stop();
        instrument.clip = noteBank[Mod(semitone, 12), Mathf.Clamp(register, 0, 2)];
        instrument.volume = gain;
        instrument.Play();
    }

    private float BeatDuration => 60f / Mathf.Max(1f, tempoBpm);

    private static float RelativePhaseDegrees(Vector3 first, Vector3 second)
    {
        float firstAngle = Mathf.Atan2(first.y, first.x) * Mathf.Rad2Deg;
        float secondAngle = Mathf.Atan2(second.y, second.x) * Mathf.Rad2Deg;
        return Mathf.Repeat(firstAngle - secondAngle + 180f, 360f) - 180f;
    }

    private static int RootFromPhase(float phaseDegrees)
    {
        int circleIndex = Mod(Mathf.RoundToInt(Mathf.Repeat(phaseDegrees, 360f) / 30f), CircleOfFifths.Length);
        return CircleOfFifths[circleIndex];
    }

    private static float Frequency(int semitone, int register)
    {
        float d3 = 146.83f;
        float registerMultiplier = register == 0 ? 0.5f : register == 2 ? 2f : 1f;
        return d3 * registerMultiplier * Mathf.Pow(2f, semitone / 12f);
    }

    private static AudioClip CreatePluckClip(string name, float frequency, int sampleRate)
    {
        const float duration = 0.48f;
        int count = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = (1f - Mathf.Exp(-t / 0.018f)) * Mathf.Exp(-6.2f * t);
            float sound = Mathf.Sin(Mathf.PI * 2f * frequency * t);
            sound += 0.05f * Mathf.Sin(Mathf.PI * 2f * frequency * 2f * t);
            samples[i] = sound * envelope * 0.26f;
        }
        AudioClip clip = AudioClip.Create(name, count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnMeasurementCompleted(MeasurementResult result)
    {
        measurementParity ^= result.Outcome & 1;
        cadenceStepsRemaining = 3;
        arpeggioIndex = 0;
    }

    private void OnDisable()
    {
        QubitManager.MeasurementCompleted -= OnMeasurementCompleted;
        QubitAudio.SuppressLegacyFm = false;
    }

    private static void StopLegacyFmSources()
    {
        QubitAudio[] legacySources = FindObjectsByType<QubitAudio>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (QubitAudio legacySource in legacySources)
        {
            legacySource.playSound = false;
            AudioSource source = legacySource.GetComponent<AudioSource>();
            if (source != null)
            {
                source.Stop();
            }
        }
    }

    private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;
}

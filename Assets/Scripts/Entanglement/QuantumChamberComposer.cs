using System;
using UnityEngine;

/// <summary>
/// A state-driven, three-voice chamber score. The quantum state selects musical
/// form rather than merely controlling oscillator values:
/// solo motifs -> pair imitation -> three-voice canon -> measurement cadence.
/// Audio is generated locally and is intentionally not networked.
/// </summary>
public sealed class QuantumChamberComposer : MonoBehaviour
{
    private const int VoiceCount = 3;
    // A pentatonic field removes the semitone/tritone collisions that made the
    // first generative pass tiring in a headset.
    private static readonly int[] PentatonicSemitones = { 0, 2, 4, 7, 9 };
    private static readonly int[] CanonPhrase = { 0, 2, 3, 4, 3, 2, 0 };

    [Header("Form")]
    [SerializeField, Range(42f, 120f)] private float tempoBpm = 54f;
    [SerializeField, Range(0.001f, 0.25f)] private float pairEntryThreshold = 0.025f;
    [SerializeField, Range(0f, 0.45f)] private float soloGain = 0.055f;
    [SerializeField, Range(0f, 0.45f)] private float pairGain = 0.085f;
    [SerializeField, Range(0f, 0.45f)] private float canonGain = 0.07f;
    [SerializeField, Range(0f, 0.5f)] private float cadenceGain = 0.12f;

    private readonly AudioSource[] voices = new AudioSource[VoiceCount];
    private AudioClip[,,] noteBank;
    private AudioClip[,] minorThirdBank;
    private float nextBeatTime;
    private int beatIndex;
    private int measuredBitMask;
    private int knownMeasurementMask;
    private int cadenceBeatsRemaining;

    private enum MusicalForm
    {
        SoloMotifs,
        PairImitation,
        ThreeVoiceCanon
    }

    private void OnEnable()
    {
        QubitManager.MeasurementCompleted += OnMeasurementCompleted;
    }

    private void Start()
    {
        int sampleRate = Mathf.Max(22050, AudioSettings.outputSampleRate);
        noteBank = new AudioClip[VoiceCount, PentatonicSemitones.Length, 3];
        minorThirdBank = new AudioClip[VoiceCount, 3];
        for (int voice = 0; voice < VoiceCount; voice++)
        {
            GameObject instrument = new GameObject($"Chamber Voice Q{voice}");
            instrument.transform.SetParent(transform, false);
            AudioSource source = instrument.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 96 + voice;
            voices[voice] = source;
            for (int degree = 0; degree < PentatonicSemitones.Length; degree++)
            {
                for (int register = 0; register < 3; register++)
                {
                    noteBank[voice, degree, register] = CreateNoteClip(
                        $"Q{voice} Degree{degree} Register{register}",
                        FrequencyFor(voice, degree, register), voice, sampleRate);
                }
            }
            for (int register = 0; register < 3; register++)
            {
                minorThirdBank[voice, register] = CreateNoteClip(
                    $"Q{voice} Minor Third Register{register}",
                    FrequencyForMinorThird(voice, register), voice, sampleRate);
            }
        }
        nextBeatTime = Time.time + BeatDuration;
    }

    private void Update()
    {
        if (noteBank == null || Time.time < nextBeatTime)
        {
            return;
        }

        // Do not burst notes after a frame hitch; resume on the next musical pulse.
        nextBeatTime = Time.time + BeatDuration;
        EntanglementSnapshot snapshot = QubitManager.GetEntanglementSnapshot();
        if (snapshot == null || snapshot.Nodes.Count < VoiceCount || snapshot.Pairs.Count < VoiceCount)
        {
            return;
        }

        if (cadenceBeatsRemaining > 0)
        {
            PlayMeasurementCadence();
            cadenceBeatsRemaining--;
            beatIndex++;
            return;
        }

        switch (DetermineForm(snapshot, out int first, out int second, out float pairStrength))
        {
            case MusicalForm.ThreeVoiceCanon:
                PlayCanon(snapshot);
                break;
            case MusicalForm.PairImitation:
                PlayPairImitation(snapshot, first, second, pairStrength);
                break;
            default:
                PlaySoloMotif(snapshot);
                break;
        }
        beatIndex++;
    }

    private float BeatDuration => 60f / Mathf.Max(1f, tempoBpm);

    private MusicalForm DetermineForm(
        EntanglementSnapshot snapshot, out int first, out int second, out float strength)
    {
        first = 0;
        second = 1;
        strength = 0f;
        if (snapshot.HasThreePartyCorrelation && snapshot.ThreePartyCorrelationStrength > pairEntryThreshold)
        {
            return MusicalForm.ThreeVoiceCanon;
        }

        for (int i = 0; i < snapshot.Pairs.Count; i++)
        {
            PairEntanglementMetric pair = snapshot.Pairs[i];
            float candidate = Mathf.Clamp01((float)pair.LogarithmicNegativity);
            if (candidate > strength)
            {
                strength = candidate;
                first = pair.FirstQubitId;
                second = pair.SecondQubitId;
            }
        }
        return strength >= pairEntryThreshold
            ? MusicalForm.PairImitation
            : MusicalForm.SoloMotifs;
    }

    private void PlaySoloMotif(EntanglementSnapshot snapshot)
    {
        // Independent, staggered entries make the pre-entangled state audible.
        int voice = beatIndex % VoiceCount;
        PlayStateNote(voice, snapshot.GetNode(voice), soloGain, 0);
    }

    private void PlayPairImitation(
        EntanglementSnapshot snapshot, int first, int second, float strength)
    {
        QubitMetric leader = snapshot.GetNode(first);
        QubitMetric follower = snapshot.GetNode(second);
        int leaderDegree = DegreeFromBloch(leader.BlochVector);
        int leaderRegister = RegisterFromBloch(leader.BlochVector);
        float mutualInformation = Mathf.Clamp01((float)snapshot.GetPair(first, second).MutualInformationBits * 0.5f);

        // A weak pair answers at a fifth; a strong, information-sharing pair
        // gradually adopts the leader's degree, then adds a close harmonic voice.
        int followerDegree = DegreeFromBloch(follower.BlochVector);
        int imitatedDegree = Mathf.RoundToInt(Mathf.Lerp(followerDegree, leaderDegree, mutualInformation));
        int harmonySteps = strength < 0.45f ? 3 : 2;
        PlayNote(first, leaderDegree, leaderRegister, pairGain * (0.65f + 0.35f * strength));
        PlayNote(second, imitatedDegree + harmonySteps, leaderRegister, pairGain * strength);

        int third = 3 - first - second;
        if (beatIndex % 4 == 0)
        {
            PlayStateNote(third, snapshot.GetNode(third), soloGain * 0.4f, -1);
        }
    }

    private void PlayCanon(EntanglementSnapshot snapshot)
    {
        // Each voice enters the same phrase one beat apart. The root is derived
        // from the mean local phase, so the canon is shaped by the live state.
        int root = MeanPhaseDegree(snapshot);
        for (int voice = 0; voice < VoiceCount; voice++)
        {
            int phraseIndex = Mod(beatIndex - voice * 2, CanonPhrase.Length);
            int register = 1 + (voice == 2 ? 1 : 0);
            float strength = Mathf.Clamp01((float)snapshot.ThreePartyCorrelationStrength);
            PlayNote(voice, root + CanonPhrase[phraseIndex], register,
                canonGain * (0.62f + 0.38f * strength));
        }
    }

    private void PlayMeasurementCadence()
    {
        // Known measurement bits select the final mode: even parity resolves
        // upward (major), odd parity folds inward (minor). This is a musical
        // rendering of the observed outcome, not an arbitrary random ending.
        bool oddParity = CountBits(measuredBitMask & knownMeasurementMask) % 2 == 1;
        int[] finalChord = oddParity ? new[] { 0, 2, 4 } : new[] { 0, 2, 4 };
        int phase = 3 - cadenceBeatsRemaining;
        if (phase < 2)
        {
            // Suspension then resolution; odd parity carries the lowered third.
            int third = oddParity ? 1 : 2;
            PlayNote(0, phase == 0 ? 4 : 0, 0, cadenceGain * 0.55f);
            if (phase == 0 || !oddParity)
            {
                PlayNote(1, phase == 0 ? 3 : third, 1, cadenceGain * 0.65f);
            }
            else
            {
                PlayMinorThird(1, 1, cadenceGain * 0.65f);
            }
            PlayNote(2, phase == 0 ? 6 : 4, 2, cadenceGain * 0.6f);
        }
        else
        {
            for (int voice = 0; voice < VoiceCount; voice++)
            {
                if (voice == 1 && oddParity)
                {
                    PlayMinorThird(voice, 1, cadenceGain);
                }
                else
                {
                    PlayNote(voice, finalChord[voice], voice == 2 ? 2 : 1, cadenceGain);
                }
            }
        }
    }

    private void PlayStateNote(int voice, QubitMetric node, float gain, int registerOffset)
    {
        PlayNote(voice, DegreeFromBloch(node.BlochVector), RegisterFromBloch(node.BlochVector) + registerOffset, gain);
    }

    private void PlayNote(int voice, int degree, int register, float gain)
    {
        voices[voice].PlayOneShot(noteBank[voice, Mod(degree, PentatonicSemitones.Length), Mathf.Clamp(register, 0, 2)], gain);
    }

    private void PlayMinorThird(int voice, int register, float gain)
    {
        voices[voice].PlayOneShot(minorThirdBank[voice, Mathf.Clamp(register, 0, 2)], gain);
    }

    private static int DegreeFromBloch(Vector3 bloch)
    {
        float azimuth = Mathf.Atan2(bloch.y, bloch.x) / (Mathf.PI * 2f);
        return Mod(Mathf.FloorToInt((azimuth + 1f) * PentatonicSemitones.Length), PentatonicSemitones.Length);
    }

    private static int RegisterFromBloch(Vector3 bloch)
    {
        return bloch.z > 0.34f ? 2 : bloch.z < -0.34f ? 0 : 1;
    }

    private static int MeanPhaseDegree(EntanglementSnapshot snapshot)
    {
        Vector2 phase = Vector2.zero;
        for (int i = 0; i < VoiceCount; i++)
        {
            Vector3 bloch = snapshot.GetNode(i).BlochVector;
            phase += new Vector2(bloch.x, bloch.y);
        }
        return DegreeFromBloch(new Vector3(phase.x, phase.y, 0f));
    }

    private static float FrequencyFor(int voice, int degree, int register)
    {
        float d3 = 146.83f;
        float registerMultiplier = register == 0 ? 0.5f : register == 2 ? 2f : 1f;
        float voiceDetune = 1f;
        return d3 * registerMultiplier * voiceDetune * Mathf.Pow(2f, PentatonicSemitones[degree] / 12f);
    }

    private static float FrequencyForMinorThird(int voice, int register)
    {
        float d3 = 146.83f;
        float registerMultiplier = register == 0 ? 0.5f : register == 2 ? 2f : 1f;
        float voiceDetune = 1f;
        return d3 * registerMultiplier * voiceDetune * Mathf.Pow(2f, 3f / 12f);
    }

    private static AudioClip CreateNoteClip(string name, float frequency, int voice, int sampleRate)
    {
        const float duration = 0.9f;
        int count = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[count];
        float attack = 0.05f + voice * 0.014f;
        float brightness = 0.06f + voice * 0.025f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = (1f - Mathf.Exp(-t / attack)) * Mathf.Exp(-4.2f * t);
            float signal = Mathf.Sin(Mathf.PI * 2f * frequency * t);
            signal += brightness * Mathf.Sin(Mathf.PI * 2f * frequency * 2f * t + voice * 0.7f);
            signal += 0.025f * Mathf.Sin(Mathf.PI * 2f * frequency * 3f * t + 0.4f);
            samples[i] = signal * envelope * 0.28f;
        }
        AudioClip clip = AudioClip.Create(name, count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnMeasurementCompleted(MeasurementResult result)
    {
        if (result.QubitId < 0 || result.QubitId >= VoiceCount)
        {
            return;
        }
        int bit = 1 << result.QubitId;
        knownMeasurementMask |= bit;
        measuredBitMask = result.Outcome == 0
            ? measuredBitMask & ~bit
            : measuredBitMask | bit;
        cadenceBeatsRemaining = 3;
    }

    private void OnDisable()
    {
        QubitManager.MeasurementCompleted -= OnMeasurementCompleted;
    }

    private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;

    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }
        return count;
    }
}

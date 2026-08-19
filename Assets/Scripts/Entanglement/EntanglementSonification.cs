using UnityEngine;

/// <summary>
/// A local, generative score for the three pairwise entanglement values.
/// It uses no recorded assets: pair E_N values drive three sustained voices,
/// while rapid changes create short harmonic gestures.
/// </summary>
public sealed class EntanglementSonification : MonoBehaviour
{
    private const int PairCount = 3;
    private static readonly float[] PairFundamentals = { 261.63f, 293.66f, 329.63f };

    [Header("Sustained Pair Voices")]
    [SerializeField, Range(0f, 0.5f)] private float pairGain = 0.16f;
    [SerializeField, Range(0.1f, 8f)] private float responseSpeed = 2.5f;
    [SerializeField, Range(1000f, 22000f)] private float openFilterFrequency = 11000f;
    [SerializeField, Range(100f, 4000f)] private float closedFilterFrequency = 750f;
    [Header("Entanglement Gestures")]
    [SerializeField, Range(0f, 0.5f)] private float gestureGain = 0.2f;
    [SerializeField, Range(0.05f, 1f)] private float gestureCooldown = 0.28f;
    [SerializeField, Range(0.01f, 0.3f)] private float gestureDeltaThreshold = 0.055f;
    [SerializeField, Range(0f, 0.35f)] private float globalGain = 0.1f;

    private readonly AudioSource[] pairSources = new AudioSource[PairCount];
    private readonly AudioLowPassFilter[] pairFilters = new AudioLowPassFilter[PairCount];
    private readonly AudioClip[] gestureClips = new AudioClip[PairCount];
    private readonly float[] strengths = new float[PairCount];
    private readonly float[] previousStrengths = new float[PairCount];
    private readonly float[] nextGestureTimes = new float[PairCount];
    private AudioSource globalSource;
    private AudioLowPassFilter globalFilter;
    private float globalStrength;

    private void Start()
    {
        int sampleRate = Mathf.Max(22050, AudioSettings.outputSampleRate);
        for (int i = 0; i < PairCount; i++)
        {
            pairSources[i] = CreateLoopVoice($"Pair Voice {i + 1}", PairFundamentals[i], sampleRate);
            pairFilters[i] = pairSources[i].gameObject.AddComponent<AudioLowPassFilter>();
            pairFilters[i].cutoffFrequency = closedFilterFrequency;
            gestureClips[i] = CreateGestureClip($"Pair Gesture {i + 1}", PairFundamentals[i], sampleRate);
        }

        globalSource = CreateLoopVoice("Three-Party Resonance", 130.81f, sampleRate);
        globalSource.volume = 0f;
        globalFilter = globalSource.gameObject.AddComponent<AudioLowPassFilter>();
        globalFilter.cutoffFrequency = closedFilterFrequency;
    }

    private void Update()
    {
        if (pairSources[0] == null)
        {
            return;
        }

        EntanglementSnapshot snapshot = QubitManager.GetEntanglementSnapshot();
        if (snapshot == null || snapshot.Pairs.Count < PairCount)
        {
            return;
        }

        float smoothing = 1f - Mathf.Exp(-responseSpeed * Time.deltaTime);
        for (int i = 0; i < PairCount; i++)
        {
            PairEntanglementMetric pair = snapshot.Pairs[i];
            float target = Mathf.Clamp01((float)pair.LogarithmicNegativity);
            strengths[i] = Mathf.Lerp(strengths[i], target, smoothing);
            UpdatePairVoice(i, strengths[i]);

            float increase = target - previousStrengths[i];
            bool entered = previousStrengths[i] <= 0.0001f && target > 0.0001f;
            if ((entered || increase >= gestureDeltaThreshold) && Time.time >= nextGestureTimes[i])
            {
                // The gesture amount follows the real state change, not grab motion.
                pairSources[i].PlayOneShot(gestureClips[i], gestureGain * Mathf.Clamp01(0.35f + increase * 3f));
                nextGestureTimes[i] = Time.time + gestureCooldown;
            }
            previousStrengths[i] = target;
        }

        float targetGlobal = snapshot.HasThreePartyCorrelation
            ? Mathf.Clamp01((float)snapshot.ThreePartyCorrelationStrength)
            : 0f;
        globalStrength = Mathf.Lerp(globalStrength, targetGlobal, smoothing);
        globalSource.volume = globalGain * Mathf.Pow(globalStrength, 1.4f);
        globalFilter.cutoffFrequency = Mathf.Lerp(closedFilterFrequency, openFilterFrequency, globalStrength);
    }

    private void UpdatePairVoice(int pairIndex, float strength)
    {
        pairSources[pairIndex].volume = pairGain * Mathf.Pow(strength, 1.35f);
        pairFilters[pairIndex].cutoffFrequency = Mathf.Lerp(
            closedFilterFrequency, openFilterFrequency, strength);
    }

    private AudioSource CreateLoopVoice(string voiceName, float fundamental, int sampleRate)
    {
        GameObject voice = new GameObject(voiceName);
        voice.transform.SetParent(transform, false);
        AudioSource source = voice.AddComponent<AudioSource>();
        source.clip = CreatePearlLoop(voiceName, fundamental, sampleRate);
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.Play();
        return source;
    }

    private static AudioClip CreatePearlLoop(string clipName, float fundamental, int sampleRate)
    {
        const float duration = 4f;
        int count = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            // Fundamental, fifth and ninth: consonant but shimmering rather than a static sine.
            float value = 0.56f * Mathf.Sin(Mathf.PI * 2f * fundamental * t);
            value += 0.24f * Mathf.Sin(Mathf.PI * 2f * fundamental * 1.5f * t + 0.35f);
            value += 0.12f * Mathf.Sin(Mathf.PI * 2f * fundamental * 2.25f * t + 1.1f);
            value += 0.08f * Mathf.Sin(Mathf.PI * 2f * (fundamental * 3f + 0.19f) * t);
            samples[i] = value * 0.42f;
        }
        AudioClip clip = AudioClip.Create(clipName, count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateGestureClip(string clipName, float fundamental, int sampleRate)
    {
        const float duration = 0.72f;
        int count = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-5.8f * t) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / duration));
            float upwardGlide = Mathf.Lerp(0.92f, 1.38f, Mathf.Clamp01(t / 0.22f));
            float value = Mathf.Sin(Mathf.PI * 2f * fundamental * upwardGlide * t);
            value += 0.45f * Mathf.Sin(Mathf.PI * 2f * fundamental * 2f * upwardGlide * t);
            samples[i] = value * envelope * 0.3f;
        }
        AudioClip clip = AudioClip.Create(clipName, count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

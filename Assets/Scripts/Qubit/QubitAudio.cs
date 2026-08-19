using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource), typeof(AudioReverbFilter))]
public class QubitAudio : MonoBehaviour
{
    /// <summary>
    /// Set by dedicated musical scenes to silence the original FM sonifier.
    /// Kept false by default so legacy scenes retain their original behaviour.
    /// </summary>
    public static bool SuppressLegacyFm { get; set; }

    private const int NumberOfNotes = 4;
    private readonly float[] frequency = new float[NumberOfNotes];
    private readonly double[] phase = new double[NumberOfNotes];
    private readonly float[] amplitude = { 1f, 0.5f, 0.33f, 0.25f };

    public AudioSource audioSource;
    public bool playSound;
    public float masterAmp;
    [Range(0, 20)] public float carrierMultiplier = 1f;
    [Range(0, 20)] public float modularMultiplier = 1f;
    public float scale = 3.3f;

    private const double SampleRate = 44100.0;
    private AudioReverbFilter reverb;
    private Qubit qubit;

    private void Start()
    {
        qubit = GetComponentInParent<Qubit>();
        audioSource = GetComponent<AudioSource>();
        reverb = GetComponent<AudioReverbFilter>();
        reverb.enabled = true;
        reverb.reverbPreset = AudioReverbPreset.User;
        audioSource.playOnAwake = false;
        audioSource.Stop();
        UpdateFrequencies(1f, 0f);
    }

    private void Update()
    {
        if (SuppressLegacyFm)
        {
            playSound = false;
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            return;
        }

        if (qubit == null)
        {
            playSound = false;
            return;
        }

        int id = qubit.GetIndex();
        EntanglementSnapshot snapshot = QubitManager.GetEntanglementSnapshot();
        if (snapshot == null || id >= snapshot.Nodes.Count)
        {
            playSound = false;
            return;
        }

        QubitMetric node = snapshot.GetNode(id);
        float maximumPairEntanglement = 0f;
        for (int pairIndex = 0; pairIndex < snapshot.Pairs.Count; pairIndex++)
        {
            PairEntanglementMetric pair = snapshot.Pairs[pairIndex];
            if (pair.FirstQubitId == id || pair.SecondQubitId == id)
            {
                maximumPairEntanglement = Mathf.Max(
                    maximumPairEntanglement, (float)pair.LogarithmicNegativity);
            }
        }
        playSound = node.Renyi2Entropy > EntanglementMetrics.CorrelationEntropyThreshold;
        float adjust = 1f + new Vector2(transform.localPosition.x, transform.localPosition.y).magnitude * 3f;
        masterAmp = Mathf.Clamp((float)(node.Renyi2Entropy / Math.Log(2.0)) * 0.08f, 0f, 0.08f);
        UpdateFrequencies(adjust, (float)snapshot.ThreePartyCorrelationStrength);

        if (playSound && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
        else if (!playSound && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        reverb.diffusion = Mathf.Clamp(maximumPairEntanglement * 100f, 0f, 100f);
    }

    private void UpdateFrequencies(float adjust, float triadStrength)
    {
        frequency[0] = 130.81f * scale / adjust;
        frequency[1] = 164.81f * scale * adjust;
        frequency[2] = 196.00f * scale * (1f + 0.25f * triadStrength);
        frequency[3] = frequency[0] * 0.5f;
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (SuppressLegacyFm || !playSound || channels < 1)
        {
            return;
        }

        for (int sample = 0; sample < data.Length; sample += channels)
        {
            double chord = 0.0;
            for (int note = 0; note < NumberOfNotes; note++)
            {
                phase[note] += Math.PI * frequency[note] / SampleRate;
                chord += amplitude[note] * FM(phase[note],
                    note == 1 ? carrierMultiplier : 1f,
                    note == 1 ? modularMultiplier : 1f);
                if (phase[note] >= Math.PI * 2.0)
                {
                    phase[note] -= Math.PI * 2.0;
                }
            }

            float value = masterAmp * (float)chord;
            for (int channel = 0; channel < channels && sample + channel < data.Length; channel++)
            {
                data[sample + channel] = value;
            }
        }
    }

    private static double FM(double phaseValue, float carrier, float modulator)
    {
        return Math.Sin(phaseValue * carrier + Math.Sin(phaseValue * modulator));
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(AudioReverbFilter))]

public class QubitAudio : MonoBehaviour
{
    public AudioSource audioSource; 
    public bool playSound = false; 
    public float masterAmp; // amplitude of audio
    static int numberOfNotes = 4;
    [Range(0, 20)]
    public float carrierMultiplier; // carrier frequency = frequency * carrierMultiplier
    [Range(0, 20)]
    public float modularMultiplier; // modular frequency = frequency * modularMultiplier
    float fundamentalFreq, initFreq;
    [Range(523f, 783f)]
    float[] frequency = new float[numberOfNotes]; // main note frequency
    float[] phase = new float[numberOfNotes];
    float[] amplitude = new float[numberOfNotes];
    static public float sampleRate = 44100;
    AudioReverbFilter reverb;
    public float scale = 3.3f; 

    void Start()
    {
        reverb = GetComponent<AudioReverbFilter>();
        reverb.enabled = true;
        reverb.reverbPreset = AudioReverbPreset.User;

        masterAmp = 0.3f;
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.Stop(); 
        carrierMultiplier = 1f;
        modularMultiplier = 1f;
        initFreq = fundamentalFreq;
        // fundamentalFreq = 75f;
        // frequency[0] = fundamentalFreq;
        // frequency[1] = fundamentalFreq * 2f;
        // frequency[2] = fundamentalFreq * 3f;
        // frequency[3] = fundamentalFreq * 4f;
        // frequency[4] = fundamentalFreq * 5f;

        fundamentalFreq = 130.81f;
        frequency[0] = 130.81f * scale;
        frequency[1] = 164.81f* scale;
        frequency[2] = 196.0f* scale;
        frequency[3] = frequency[0]/2f;

        amplitude[0] = 1f;
        amplitude[1] = 0.5f;
        amplitude[2] = 0.33f;
        amplitude[3] = 0.25f;
    }
    void Update()
    {
        playSound = Entanglement.entangled[0];
        // Frequency 
        fundamentalFreq = initFreq * ( 1f + (transform.position.z) / 0.25f) * (0.63f);
        float adjust = 1 + new Vector2(transform.localPosition.x, transform.localPosition.y).magnitude * 3f;         
        masterAmp = Entanglement.qubits[0].transform.localScale.x/5f;
        if (playSound && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
        else if (!playSound && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        reverb.diffusion = QubitManager.J[0]* 30f;

        frequency[0] = 130.81f * scale / adjust;
        frequency[1] = 164.81f* scale * adjust;
        frequency[2] = 196.0f* scale;
        frequency[3] = frequency[0]/2f;


    }
    void OnAudioFilterRead(float[] data, int channels)
    {
        if(playSound){
            for (int i = 0; i < data.Length; i += channels)
            {
                float[] freqinphase = new float[numberOfNotes];
                for (int j = 0; j < numberOfNotes; j++)
                {
                    freqinphase[j] = Mathf.PI * frequency[j] / sampleRate;
                }
                phase[0] += freqinphase[0];
                phase[1] += freqinphase[1];
                phase[2] += freqinphase[2];
                phase[3] += freqinphase[3];

                // stack chords
                float chords = 0;
                //for (int j = 0; j < numberOfNotes; j++)
                //{
                //    chords += amplitude[j] * FM(phase[j], carrierMultiplier, modularMultiplier);
                //}
                chords += amplitude[0] * FM(phase[0], 1, 1);
                chords += amplitude[1] * FM(phase[1], carrierMultiplier, modularMultiplier);
                chords += amplitude[2] * FM(phase[2], 1, 1);
                chords += amplitude[3] * FM(phase[3], carrierMultiplier, modularMultiplier);

                data[i] = masterAmp * chords;
                data[i + 1] = data[i];

                // Reset phase
                for (int j = 0; j < numberOfNotes; j++)
                {
                    if (phase[j] >= 2 * Mathf.PI)
                    {
                        phase[j] -= 2 * Mathf.PI;
                    }
                }
            }
        }

    }
    public float FM(float phase, float carMul, float modMul)
    {
        //return Mathf.Sin(ComputeFreq(phase) * timeIdx); // Sine wave
        return Mathf.Sin(phase * carMul + Mathf.Sin(phase * modMul)); // fluctuating FM
    }

}

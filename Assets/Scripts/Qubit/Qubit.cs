using Complex = System.Numerics.Complex;
using UnityEngine;
using UnityEngine.VFX;
using Random = UnityEngine.Random;
using static Gates;
using static QubitManager;

public class Qubit : MonoBehaviour
{
    public VisualEffect HadamardEffect, PauliXEffect, PauliZEffect, PhaseSEffect;
    public VisualEffectAsset HadamardEffectAsset, PauliXEffectAsset, PauliZEffectAsset, PhaseSEffectAsset;

    public GameObject HadamardGuide, PauliXGuide, PhaseSGuide, PauliZGuide;

    [SerializeField] private GameObject dot;
    [SerializeField] private LineRenderer lineRenderer;

    private ComplexMatrix identityMatrix;
    private ComplexMatrix pauliX;
    private ComplexMatrix pauliZ;
    private ComplexMatrix hadamard;
    private ComplexMatrix phaseS;
    private ComplexMatrix phaseSDagger;

    private int initQubits;
    public int index;
    private AudioSource audioSource;

    [SerializeField]
    public AudioClip audioClipH, audioClipX, audioClipZ, audioClipS;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        UpdateDensityMatrix();
        IncrementInitQubits();
        index = GetInitQubits() - 1;
        initQubits = GetInitQubits();

        identityMatrix = IdentityMatrix();
        pauliX = initQubits == 1 ? PauliX() : IdentityMatrix();
        pauliZ = initQubits == 1 ? PauliZ() : IdentityMatrix();
        hadamard = initQubits == 1 ? Hadamard() : IdentityMatrix();
        phaseS = initQubits == 1 ? PhaseS() : IdentityMatrix();
        phaseSDagger = initQubits == 1 ? PhaseSDagger() : IdentityMatrix();

        for (int i = 2; i <= initQubits; i++)
        {
            identityMatrix = identityMatrix.KroneckerProduct(IdentityMatrix());
            pauliX = pauliX.KroneckerProduct(initQubits == i ? PauliX() : IdentityMatrix());
            pauliZ = pauliZ.KroneckerProduct(initQubits == i ? PauliZ() : IdentityMatrix());
            hadamard = hadamard.KroneckerProduct(initQubits == i ? Hadamard() : IdentityMatrix());
            phaseS = phaseS.KroneckerProduct(initQubits == i ? PhaseS() : IdentityMatrix());
            phaseSDagger = phaseSDagger.KroneckerProduct(initQubits == i ? PhaseSDagger() : IdentityMatrix());
        }

        for (int i = 0; i < GetQubits() - initQubits; i++)
        {
            identityMatrix = identityMatrix.KroneckerProduct(IdentityMatrix());
            pauliX = pauliX.KroneckerProduct(IdentityMatrix());
            pauliZ = pauliZ.KroneckerProduct(IdentityMatrix());
            hadamard = hadamard.KroneckerProduct(IdentityMatrix());
            phaseS = phaseS.KroneckerProduct(IdentityMatrix());
            phaseSDagger = phaseSDagger.KroneckerProduct(IdentityMatrix());
        }

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, dot.transform.position);
        }
    }

    private void Update()
    {
        if (initQubits != GetInitQubits())
        {
            initQubits += 1;
            identityMatrix = identityMatrix.KroneckerProduct(IdentityMatrix());
            pauliX = pauliX.KroneckerProduct(IdentityMatrix());
            pauliZ = pauliZ.KroneckerProduct(IdentityMatrix());
            hadamard = hadamard.KroneckerProduct(IdentityMatrix());
            phaseS = phaseS.KroneckerProduct(IdentityMatrix());
            phaseSDagger = phaseSDagger.KroneckerProduct(IdentityMatrix());
        }

        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, dot.transform.position);
        }

        UpdatePosition();
    }

    public ComplexMatrix GetIdentityMatrix()
    {
        return identityMatrix;
    }

    public ComplexMatrix GetPauliX()
    {
        return pauliX;
    }

    public ComplexMatrix GetPauliZ()
    {
        return pauliZ;
    }

    public ComplexMatrix GetHadamard()
    {
        return hadamard;
    }

    public ComplexMatrix GetPhaseS()
    {
        return phaseS;
    }

    public ComplexMatrix GetPhaseSDagger()
    {
        return phaseSDagger;
    }

    public int GetIndex()
    {
        return index;
    }

    public void UpdatePosition()
    {
        if (GetDensityMatrix().Columns > 2)
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
            ComplexMatrix matrix = GetDensityMatrix();
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
                Instantiate(HadamardGuide, spawnPos, Quaternion.identity, transform);
                break;

            case "Pauli-X":
                ApplyPauliX(this);
                audioSource.PlayOneShot(audioClipX, 1f);
                Instantiate(PauliXGuide, spawnPos, Quaternion.identity, transform);
                break;

            case "Pauli-Z":
                ApplyPauliZ(this);
                audioSource.PlayOneShot(audioClipZ, 1f);
                Instantiate(PauliZGuide, spawnPos, Quaternion.identity, transform);
                break;

            case "Phase-S":
                ApplyPhaseGate(this);
                audioSource.PlayOneShot(audioClipS, 1f);
                Instantiate(PhaseSGuide, spawnPos, Quaternion.identity, transform);
                break;

            default:
                break;
        }
    }
}



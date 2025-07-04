using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
using static QubitManager;
using static Gates;
using NumpyDotNet;
using System.Dynamic;
using UnityEngine.VFX;

public class Qubit : MonoBehaviour
{
    public VisualEffect HadamardEffect, PauliXEffect, PauliZEffect, PhaseSEffect;
    public VisualEffectAsset HadamardEffectAsset, PauliXEffectAsset, PauliZEffectAsset, PhaseSEffectAsset;

    public GameObject HadamardGuide, PauliXGuide, PhaseSGuide, PauliZGuide;

    [SerializeField] GameObject dot;
    [SerializeField] LineRenderer lineRenderer;
    private Matrix<Complex32> identityMatrix;
    private Matrix<Complex32> pauliX;
    private Matrix<Complex32> pauliZ;
    private Matrix<Complex32> hadamard;
    private Matrix<Complex32> phaseS;
    private Matrix<Complex32> phaseSDagger;
    private int initQubits;
    public int index;
    AudioSource audioSource;

    [SerializeField]
    public AudioClip audioClipH, audioClipX, audioClipZ, audioClipS;

    public void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        UpdateDensityMatrix();
        IncrementInitQubits();
        index = GetInitQubits() - 1;
        initQubits = GetInitQubits();
        identityMatrix = IdentityMatrix();
        pauliX = (initQubits == 1) ? PauliX() : IdentityMatrix();
        pauliZ = (initQubits == 1) ? PauliZ() : IdentityMatrix();
        hadamard = (initQubits == 1) ? Hadamard() : IdentityMatrix();
        phaseS = (initQubits == 1) ? PhaseS() : IdentityMatrix();
        phaseSDagger = (initQubits == 1) ? PhaseSDagger() : IdentityMatrix();
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

    public void Update()
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


    public Matrix<Complex32> GetIdentityMatrix()
    {
        return identityMatrix;
    }
    public Matrix<Complex32> GetPauliX()
    {
        return pauliX;
    }
    public Matrix<Complex32> GetPauliZ()
    {
        return pauliZ;
    }
    public Matrix<Complex32> GetHadamard()
    {
        return hadamard;
    }
    public Matrix<Complex32> GetPhaseS()
    {
        return phaseS;
    }
    public Matrix<Complex32> GetPhaseSDagger()
    {
        return phaseSDagger;
    }

    public int GetIndex()
    {
        return index;
    }

    public void UpdatePosition()
    {
        if (GetDensityMatrix().ColumnCount > 2)
        {
            ndarray array = PartialTrace(index);
            Complex32 p10 = (Complex32)array[1, 0];
            Complex32 p01 = (Complex32)array[0, 1];
            Complex32 p00 = (Complex32)array[0, 0];
            Complex32 p11 = (Complex32)array[1, 1];
            dot.transform.localPosition = new Vector3(2 * p01.Real, 2 * p10.Imaginary, p00.Real - p11.Real) / 2;
            dot.transform.LookAt(transform);
            // Debug.Log(dot.transform.localPosition);
        }
        else
        {
            Matrix<Complex32> matrix = GetDensityMatrix();
            Complex32 p10 = matrix[1, 0];
            Complex32 p01 = matrix[0, 1];
            Complex32 p00 = matrix[0, 0];
            Complex32 p11 = matrix[1, 1];
            dot.transform.localPosition = new Vector3(2 * p01.Real, 2 * p10.Imaginary, p00.Real - p11.Real) / 2;
            dot.transform.LookAt(transform);
        }
    }


    void OnTriggerEnter(Collider other)
    {
        Vector3 spawnPos = this.transform.position + new Vector3(0, 0.2f, 0);

        switch (other.name)
        {
            case "Hadamard":
                print(other.name);
                ApplyHadamard(this);
                audioSource.PlayOneShot(audioClipH, 1f);
                Instantiate(HadamardGuide, spawnPos, Quaternion.identity, this.transform);
                break;

            case "Pauli-X":
                print(other.name);
                ApplyPauliX(this);
                audioSource.PlayOneShot(audioClipX, 1f);
                Instantiate(PauliXGuide, spawnPos, Quaternion.identity, this.transform);
                break;

            case "Pauli-Z":
                print(other.name);
                ApplyPauliZ(this);
                audioSource.PlayOneShot(audioClipZ, 1f);
                Instantiate(PauliZGuide, spawnPos, Quaternion.identity, this.transform);
                break;

            case "Phase-S":
                print(other.name);
                ApplyPhaseGate(this);
                audioSource.PlayOneShot(audioClipS, 1f);
                Instantiate(PhaseSGuide, spawnPos, Quaternion.identity, this.transform);
                break;

            default:
                print("None");
                break;

        }
    }

}
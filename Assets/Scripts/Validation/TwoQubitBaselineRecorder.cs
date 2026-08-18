using System.Collections;
using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using UnityEngine;

/// <summary>
/// Records non-mutating snapshots of the current two-qubit runtime state for Phase 0.
/// Add this to the QubitManager object and enable Record On Start when collecting a baseline.
/// </summary>
public class TwoQubitBaselineRecorder : MonoBehaviour
{
    [SerializeField] private bool recordOnStart;
    [SerializeField] private float[] captureDelays = { 0f, 0.5f, 1f, 2f };

    private IEnumerator Start()
    {
        if (!recordOnStart)
        {
            yield break;
        }

        float previousDelay = 0f;
        foreach (float delay in captureDelays)
        {
            float targetDelay = Mathf.Max(0f, delay);
            if (targetDelay < previousDelay)
            {
                Debug.LogWarning("[QIXR Phase 0] Capture delays must be ascending; skipping an out-of-order entry.");
                continue;
            }

            float waitTime = targetDelay - previousDelay;
            if (waitTime > 0f)
            {
                yield return new WaitForSeconds(waitTime);
            }

            LogSnapshot($"t+{targetDelay.ToString("0.###", CultureInfo.InvariantCulture)}s");
            previousDelay = targetDelay;
        }
    }

    [ContextMenu("Log Two-Qubit Baseline Snapshot")]
    public void LogSnapshotNow()
    {
        LogSnapshot("manual");
    }

    private static void LogSnapshot(string label)
    {
        if (QubitManager.GetQubits() != 2 || QubitManager.GetDensityMatrix() == null)
        {
            Debug.LogWarning($"[QIXR Phase 0] Snapshot '{label}' skipped: expected an initialized two-qubit scene, found {QubitManager.GetQubits()} qubit(s).");
            return;
        }

        ComplexMatrix rho = QubitManager.GetDensityMatrix();
        ComplexMatrix rho0 = QubitManager.PartialTrace(0);
        ComplexMatrix rho1 = QubitManager.PartialTrace(1);
        double purity = (rho * rho).Trace().Real;

        var report = new StringBuilder();
        report.AppendLine($"[QIXR Phase 0] TWO_QUBIT_SNAPSHOT label={label}");
        report.AppendLine($"trace={Format(rho.Trace())}; purity={purity.ToString("G17", CultureInfo.InvariantCulture)}");
        report.AppendLine($"rho={FormatMatrix(rho)}");
        report.AppendLine($"rho0={FormatMatrix(rho0)}; S2_0={QubitManager.Entropy(0).ToString("G17", CultureInfo.InvariantCulture)}");
        report.AppendLine($"rho1={FormatMatrix(rho1)}; S2_1={QubitManager.Entropy(1).ToString("G17", CultureInfo.InvariantCulture)}");
        Debug.Log(report.ToString());
    }

    private static string FormatMatrix(ComplexMatrix matrix)
    {
        var result = new StringBuilder("[");
        for (int row = 0; row < matrix.Rows; row++)
        {
            if (row > 0)
            {
                result.Append(';');
            }

            for (int column = 0; column < matrix.Columns; column++)
            {
                if (column > 0)
                {
                    result.Append(',');
                }

                result.Append(Format(matrix[row, column]));
            }
        }

        return result.Append(']').ToString();
    }

    private static string Format(Complex value)
    {
        return $"({value.Real.ToString("G9", CultureInfo.InvariantCulture)},{value.Imaginary.ToString("G9", CultureInfo.InvariantCulture)})";
    }
}

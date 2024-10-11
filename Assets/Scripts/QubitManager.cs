using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
using System.Collections.Generic;
using System.Linq;
using NumpyDotNet;
using static Gates;
using System;
public class QubitManager : MonoBehaviour
{
    static private Matrix<Complex32> densityMatrix;
    static int numQubits = 0;
    static int initQubits = 0;

    public static void UpdateDensityMatrix()
    {
        if (densityMatrix == null)
        {
            densityMatrix = Matrix<Complex32>.Build.DenseOfArray(new Complex32[,]
            {
                { 1, 0 },
                { 0, 0 }
            });
        }
        else
        {
            densityMatrix = densityMatrix.KroneckerProduct(UpMatrix());
        }
        numQubits++;
    }

    public static Matrix<Complex32> GetDensityMatrix()
    {
        return densityMatrix;
    }
    public static int GetQubits()
    {
        return numQubits;
    }

    public static int GetInitQubits()
    {
        return initQubits;
    }

    public static void IncrementInitQubits()
    {
        initQubits += 1;
    }

    public static void ApplyPauliX(Qubit qubit)
    {
        densityMatrix = qubit.GetPauliX() * densityMatrix * qubit.GetPauliX();
    }
    public static void ApplyPauliZ(Qubit qubit)
    {
        densityMatrix = qubit.GetPauliZ() * densityMatrix * qubit.GetPauliZ();
    }
    public static void ApplyHadamard(Qubit qubit)
    {
        densityMatrix = qubit.GetHadamard() * densityMatrix * qubit.GetHadamard();
    }
    public static void ApplyPhaseGate(Qubit qubit)
    {
        densityMatrix = qubit.GetPhaseS() * densityMatrix * qubit.GetPhaseSDagger();
    }

    public static ndarray PartialTrace(int index)
    {
        List<int> result = new List<int>();
        List<int> qtrace = new List<int> {};
        List<int> sel = new List<int> {index};
        Complex32[,] array = densityMatrix.ToArray();
        int[] dims = new int[2 * numQubits];
        int nd = numQubits;

        for(int i = 0; i < 2 * numQubits; i++)
        {
            dims[i] = 2;
        }
        for (int i = 0; i < nd; i++)
        {
            if (i != index)
            {
                qtrace.Add(i);
            }
        }
        //Based on Qutip's partial trace
        result.AddRange(qtrace);
        result.AddRange(qtrace.Select(q => nd + q));
        result.AddRange(sel);
        result.AddRange(sel.Select(q => nd + q));
        long[] positions = result.Select(i => (long)i).ToArray();

        ndarray matrix = np.array(array);
        ndarray rhomat = np.trace(matrix.reshape(new shape(dims))
                        .Transpose(positions));
        while(rhomat.shape != new shape(2,2))
        {
            rhomat = np.trace(rhomat);
        }  
        return rhomat;
    }
}
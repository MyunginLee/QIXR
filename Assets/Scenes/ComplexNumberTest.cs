using Complex = System.Numerics.Complex;
using TMPro;
using UnityEngine;

public class ComplexNumberTest : MonoBehaviour
{
    public TMP_Text textMeshPro;

    private void Update()
    {
        var data = new Complex[3, 3];
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                float real = UnityEngine.Random.Range(0f, 10f);
                float imaginary = UnityEngine.Random.Range(0f, 10f);
                data[row, col] = new Complex(real, imaginary);
            }
        }

        ComplexMatrix matrix = ComplexMatrix.FromArray(data);
        textMeshPro.text = ComplexMatrixToString(matrix);
    }

    private string ComplexMatrixToString(ComplexMatrix matrix)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int row = 0; row < matrix.Rows; row++)
        {
            for (int col = 0; col < matrix.Columns; col++)
            {
                Complex value = matrix[row, col];
                builder.AppendFormat("{0:0.00} + {1:0.00}i\t", value.Real, value.Imaginary);
            }
            builder.AppendLine();
        }
        return builder.ToString();
    }
}


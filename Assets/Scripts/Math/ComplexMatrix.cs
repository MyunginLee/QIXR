using System;
using System.Numerics;

public class ComplexMatrix
{
    private readonly Complex[,] data;

    public int Rows { get; }
    public int Columns { get; }

    public ComplexMatrix(int rows, int columns)
    {
        if (rows <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rows));
        }
        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        Rows = rows;
        Columns = columns;
        data = new Complex[rows, columns];
    }

    public Complex this[int row, int column]
    {
        get
        {
            return data[row, column];
        }
        set
        {
            data[row, column] = value;
        }
    }

    public static ComplexMatrix FromArray(Complex[,] source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        int rows = source.GetLength(0);
        int columns = source.GetLength(1);
        var matrix = new ComplexMatrix(rows, columns);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                matrix.data[r, c] = source[r, c];
            }
        }

        return matrix;
    }

    public static ComplexMatrix Identity(int size)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        var matrix = new ComplexMatrix(size, size);
        for (int i = 0; i < size; i++)
        {
            matrix.data[i, i] = Complex.One;
        }
        return matrix;
    }

    public Complex[,] ToArray()
    {
        var clone = new Complex[Rows, Columns];
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                clone[r, c] = data[r, c];
            }
        }
        return clone;
    }

    public Complex Trace()
    {
        if (Rows != Columns)
        {
            throw new InvalidOperationException("Trace requires a square matrix.");
        }

        Complex sum = Complex.Zero;
        for (int i = 0; i < Rows; i++)
        {
            sum += data[i, i];
        }
        return sum;
    }

    public ComplexMatrix ConjugateTranspose()
    {
        var result = new ComplexMatrix(Columns, Rows);
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                result.data[c, r] = Complex.Conjugate(data[r, c]);
            }
        }
        return result;
    }

    public ComplexMatrix Add(ComplexMatrix other)
    {
        EnsureSameShape(other);
        var result = new ComplexMatrix(Rows, Columns);
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                result.data[r, c] = data[r, c] + other.data[r, c];
            }
        }
        return result;
    }

    public ComplexMatrix Subtract(ComplexMatrix other)
    {
        EnsureSameShape(other);
        var result = new ComplexMatrix(Rows, Columns);
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                result.data[r, c] = data[r, c] - other.data[r, c];
            }
        }
        return result;
    }

    public ComplexMatrix Multiply(ComplexMatrix other)
    {
        if (Columns != other.Rows)
        {
            throw new InvalidOperationException("Matrix dimensions do not align for multiplication.");
        }

        var result = new ComplexMatrix(Rows, other.Columns);
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < other.Columns; c++)
            {
                Complex sum = Complex.Zero;
                for (int k = 0; k < Columns; k++)
                {
                    sum += data[r, k] * other.data[k, c];
                }
                result.data[r, c] = sum;
            }
        }
        return result;
    }

    public ComplexMatrix Multiply(Complex scalar)
    {
        var result = new ComplexMatrix(Rows, Columns);
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                result.data[r, c] = data[r, c] * scalar;
            }
        }
        return result;
    }

    public ComplexMatrix Multiply(double scalar)
    {
        return Multiply(new Complex(scalar, 0.0));
    }

    public ComplexMatrix Divide(double scalar)
    {
        if (Math.Abs(scalar) < double.Epsilon)
        {
            throw new DivideByZeroException();
        }
        return Multiply(1.0 / scalar);
    }

    public ComplexMatrix KroneckerProduct(ComplexMatrix other)
    {
        var result = new ComplexMatrix(Rows * other.Rows, Columns * other.Columns);
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                Complex value = data[r, c];
                for (int or = 0; or < other.Rows; or++)
                {
                    for (int oc = 0; oc < other.Columns; oc++)
                    {
                        int row = r * other.Rows + or;
                        int col = c * other.Columns + oc;
                        result.data[row, col] = value * other.data[or, oc];
                    }
                }
            }
        }
        return result;
    }

    public ComplexMatrix Clone()
    {
        return FromArray(ToArray());
    }

    private void EnsureSameShape(ComplexMatrix other)
    {
        if (Rows != other.Rows || Columns != other.Columns)
        {
            throw new InvalidOperationException("Matrix dimensions must match.");
        }
    }

    public static ComplexMatrix operator +(ComplexMatrix left, ComplexMatrix right)
    {
        return left.Add(right);
    }

    public static ComplexMatrix operator -(ComplexMatrix left, ComplexMatrix right)
    {
        return left.Subtract(right);
    }

    public static ComplexMatrix operator *(ComplexMatrix left, ComplexMatrix right)
    {
        return left.Multiply(right);
    }

    public static ComplexMatrix operator *(ComplexMatrix matrix, Complex scalar)
    {
        return matrix.Multiply(scalar);
    }

    public static ComplexMatrix operator *(Complex scalar, ComplexMatrix matrix)
    {
        return matrix.Multiply(scalar);
    }

    public static ComplexMatrix operator *(ComplexMatrix matrix, double scalar)
    {
        return matrix.Multiply(scalar);
    }

    public static ComplexMatrix operator *(double scalar, ComplexMatrix matrix)
    {
        return matrix.Multiply(scalar);
    }

    public static ComplexMatrix operator /(ComplexMatrix matrix, double scalar)
    {
        return matrix.Divide(scalar);
    }
}

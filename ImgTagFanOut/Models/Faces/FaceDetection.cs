using System;
using System.Collections.Generic;

namespace ImgTagFanOut.Models.Faces;

/// <summary>
/// A detected face: bounding box, detection score and five landmarks
/// (left eye, right eye, nose, left mouth corner, right mouth corner)
/// as x0, y0, x1, y1, ...
/// </summary>
public sealed record FaceDetection(
    float X,
    float Y,
    float Width,
    float Height,
    float Score,
    IReadOnlyList<float> Landmarks);

/// <summary>Converts landmarks to and from the stored format.</summary>
public static class FaceLandmarks
{
    public const int PointCount = 5;
    public const int ValueCount = PointCount * 2;

    public static byte[] Encode(IReadOnlyList<float> values)
    {
        if (values.Count != ValueCount)
        {
            throw new ArgumentException($"Expected {ValueCount} values, got {values.Count}.", nameof(values));
        }

        byte[] bytes = new byte[ValueCount * sizeof(float)];
        for (int i = 0; i < ValueCount; i++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(i * sizeof(float)), values[i]);
        }

        return bytes;
    }

    public static float[] Decode(byte[] bytes)
    {
        if (bytes.Length != ValueCount * sizeof(float))
        {
            throw new ArgumentException($"Expected {ValueCount * sizeof(float)} bytes, got {bytes.Length}.", nameof(bytes));
        }

        float[] values = new float[ValueCount];
        for (int i = 0; i < ValueCount; i++)
        {
            values[i] = BitConverter.ToSingle(bytes, i * sizeof(float));
        }

        return values;
    }
}

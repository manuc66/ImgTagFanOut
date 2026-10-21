using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace ImgTagFanOut.Models.Faces;

/// <summary>One unnamed face, with the photo it comes from and its normalized box.</summary>
public sealed record FaceEmbedding(
    int FaceId,
    string ItemName,
    float X,
    float Y,
    float Width,
    float Height,
    byte[] Embedding);

/// <summary>A group of faces that look like the same person, none of them named yet.</summary>
public sealed record FaceCluster(IReadOnlyList<FaceEmbedding> Faces)
{
    public int Count => Faces.Count;

    /// <summary>The face used as the cluster thumbnail: the first one found.</summary>
    public FaceEmbedding Representative => Faces[0];
}

/// <summary>
/// Groups unnamed faces so the user can name a dozen people instead of a hundred
/// faces. Greedy leader clustering: each face joins the closest cluster whose
/// centroid is above the threshold, otherwise it starts a new one.
/// </summary>
public sealed class FaceClusterer
{
    /// <summary>
    /// Cosine similarity above which two faces are considered the same person.
    /// Higher than the suggestion threshold: merging two people in a cluster is
    /// worse than leaving two clusters for the same person, because naming the
    /// wrong cluster tags every photo in it.
    /// </summary>
    public const float DefaultThreshold = 0.55f;

    /// <summary>A single face is not worth reviewing, it is just an unknown.</summary>
    public const int DefaultMinimumSize = 2;

    /// <summary>Bounds the work done on a very large library.</summary>
    public const int MaxFaces = 5000;

    public IReadOnlyList<FaceCluster> Cluster(
        IReadOnlyList<FaceEmbedding> faces,
        float threshold = DefaultThreshold,
        int minimumSize = DefaultMinimumSize)
    {
        List<List<FaceEmbedding>> clusters = new();
        List<float[]> centroids = new();

        foreach (FaceEmbedding face in faces.Take(MaxFaces))
        {
            float[] vector = ToFloats(face.Embedding);

            int best = -1;
            float bestScore = threshold;

            for (int i = 0; i < clusters.Count; i++)
            {
                float score = Dot(centroids[i], vector);
                if (score >= bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }

            if (best < 0)
            {
                clusters.Add([face]);
                centroids.Add(vector);
            }
            else
            {
                clusters[best].Add(face);
                centroids[best] = Normalize(Add(centroids[best], vector));
            }
        }

        return clusters
            .Where(cluster => cluster.Count >= minimumSize)
            .OrderByDescending(cluster => cluster.Count)
            .Select(cluster => new FaceCluster(cluster))
            .ToList();
    }

    private static float[] ToFloats(byte[] embedding)
    {
        float[] values = new float[embedding.Length / sizeof(float)];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadSingleLittleEndian(embedding.AsSpan(i * sizeof(float)));
        }

        return values;
    }

    private static float[] Add(float[] a, float[] b)
    {
        float[] sum = new float[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            sum[i] = a[i] + b[i];
        }

        return sum;
    }

    private static float[] Normalize(float[] values)
    {
        double norm = 0;
        foreach (float value in values)
        {
            norm += (double)value * value;
        }

        norm = Math.Sqrt(norm);
        if (norm < 1e-9)
        {
            return values;
        }

        float[] normalized = new float[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            normalized[i] = (float)(values[i] / norm);
        }

        return normalized;
    }

    private static float Dot(float[] a, float[] b)
    {
        int length = Math.Min(a.Length, b.Length);
        float sum = 0;
        for (int i = 0; i < length; i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }
}

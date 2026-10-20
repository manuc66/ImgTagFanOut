using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ImgTagFanOut.Dao;

namespace ImgTagFanOut.Models.Faces;

/// <summary>
/// Compares an unknown face with the faces already named by the user.
/// </summary>
public interface IPersonSuggester
{
    /// <summary>Best known persons for this embedding, most similar first. Empty when unsure.</summary>
    IReadOnlyList<PersonMatch> Suggest(string folder, byte[] embedding);

    /// <summary>Drops the cached gallery, call it after naming or forgetting a face.</summary>
    void Invalidate(string folder);
}

/// <param name="PersonName">Tag naming the person.</param>
/// <param name="Score">Cosine similarity, between -1 and 1.</param>
public readonly record struct PersonMatch(string PersonName, float Score);

/// <summary>
/// Exact nearest neighbours over the labelled faces, not a centroid: with a
/// handful of photos per person, the closest example beats an average that
/// blends a child and an adult. The gallery is small by nature (a family
/// album), so a full scan per face is nothing.
/// </summary>
public sealed class PersonSuggester : IPersonSuggester
{
    /// <summary>
    /// Cosine thresholds measured on LFW with SFace: 0.36 is the vendor's
    /// suggested "same person" value, 0.28 is the weakest score worth showing.
    /// </summary>
    public const float StrongThreshold = 0.36f;

    public const float WeakThreshold = 0.28f;

    /// <summary>Above this many labelled faces, the oldest are ignored to bound memory and time.</summary>
    private const int MaxGallerySize = 5000;

    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ConcurrentDictionary<string, Gallery> _galleries = new(StringComparer.Ordinal);

    public PersonSuggester(IUnitOfWorkFactory unitOfWorkFactory)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
    }

    public IReadOnlyList<PersonMatch> Suggest(string folder, byte[] embedding)
    {
        Gallery gallery = _galleries.GetOrAdd(folder, Load);
        if (gallery.Vectors.Length == 0)
        {
            return [];
        }

        float[] query = ToFloats(embedding);
        Dictionary<string, float> best = new(StringComparer.Ordinal);

        for (int i = 0; i < gallery.Vectors.Length; i++)
        {
            float score = Dot(query, gallery.Vectors[i]);
            string person = gallery.Names[i];
            if (!best.TryGetValue(person, out float current) || score > current)
            {
                best[person] = score;
            }
        }

        return best
            .Where(pair => pair.Value >= WeakThreshold)
            .OrderByDescending(pair => pair.Value)
            .Take(3)
            .Select(pair => new PersonMatch(pair.Key, pair.Value))
            .ToArray();
    }

    public void Invalidate(string folder)
    {
        _galleries.TryRemove(folder, out _);
    }

    private Gallery Load(string folder)
    {
        using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(folder);

        List<PersonEmbedding> rows = unitOfWork.FaceRepository.GetPersonEmbeddings();

        if (rows.Count > MaxGallerySize)
        {
            rows = rows.Skip(rows.Count - MaxGallerySize).ToList();
        }

        return new Gallery(
            rows.Select(row => row.PersonName).ToArray(),
            rows.Select(row => ToFloats(row.Embedding)).ToArray());
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

    private sealed record Gallery(string[] Names, float[][] Vectors);
}

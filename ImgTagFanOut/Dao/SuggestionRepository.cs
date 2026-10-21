using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Dao;

public sealed class SuggestionRepository : ISuggestionRepository
{
    private readonly IImgTagFanOutDbContext _dbContext;

    public SuggestionRepository(IImgTagFanOutDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyList<TagSuggestionDao> GetSuggestions(string itemName)
    {
        return _dbContext.TagSuggestions
            .Where(suggestion => suggestion.Item.Name == itemName)
            .OrderByDescending(suggestion => suggestion.Score)
            .ToList();
    }

    public IReadOnlyList<string> GetRejectedTags(string itemName)
    {
        return _dbContext.TagSuggestions
            .Where(suggestion => suggestion.Item.Name == itemName && suggestion.State == TagSuggestionState.Rejected)
            .Select(suggestion => suggestion.TagName)
            .ToList();
    }

    public void ReplacePending(
        int itemId,
        string sourceHash,
        string engineVersion,
        IReadOnlyList<TagSuggestionRecord> suggestions)
    {
        List<TagSuggestionDao> existing = _dbContext.TagSuggestions
            .Where(suggestion => suggestion.ItemForeignKey == itemId)
            .ToList();

        HashSet<string> rejected = existing
            .Where(suggestion => suggestion.State == TagSuggestionState.Rejected)
            .Select(suggestion => suggestion.TagName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        HashSet<string> desired = suggestions
            .Select(suggestion => suggestion.TagName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Pending rows that are not proposed anymore go away; accepted and
        // rejected rows are the feedback history and stay.
        List<TagSuggestionDao> stalePending = existing
            .Where(suggestion => suggestion.State == TagSuggestionState.Pending && !desired.Contains(suggestion.TagName))
            .ToList();
        if (stalePending.Count > 0)
        {
            _dbContext.TagSuggestions.RemoveRange(stalePending);
        }

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (TagSuggestionRecord suggestion in suggestions)
        {
            if (rejected.Contains(suggestion.TagName) || !seen.Add(suggestion.TagName))
            {
                continue;
            }

            TagSuggestionDao? row = existing.FirstOrDefault(
                candidate => string.Equals(candidate.TagName, suggestion.TagName, StringComparison.OrdinalIgnoreCase));

            if (row is null)
            {
                _dbContext.TagSuggestions.Add(new TagSuggestionDao
                {
                    ItemForeignKey = itemId,
                    TagName = suggestion.TagName,
                    Score = suggestion.Score,
                    Source = suggestion.Source,
                    ReasonArgument = suggestion.ReasonArgument,
                    SourceHash = sourceHash,
                    EngineVersion = engineVersion,
                    State = TagSuggestionState.Pending,
                    SuggestedAtUtc = DateTime.UtcNow,
                });
                continue;
            }

            // An accepted tag that was later removed from the photo can be
            // proposed again: reuse the row instead of inserting a duplicate key.
            row.TagName = suggestion.TagName;
            row.Score = suggestion.Score;
            row.Source = suggestion.Source;
            row.ReasonArgument = suggestion.ReasonArgument;
            row.SourceHash = sourceHash;
            row.EngineVersion = engineVersion;
            row.State = TagSuggestionState.Pending;
            row.SuggestedAtUtc = DateTime.UtcNow;
        }
    }

    public void SetState(string itemName, string tagName, TagSuggestionState state)
    {
        TagSuggestionDao? suggestion = _dbContext.TagSuggestions
            .FirstOrDefault(row => row.Item.Name == itemName && row.TagName == tagName);

        if (suggestion is not null)
        {
            suggestion.State = state;
        }
    }

    public ItemEmbeddingDao? GetEmbedding(int itemId)
    {
        return _dbContext.ItemEmbeddings.FirstOrDefault(embedding => embedding.ItemForeignKey == itemId);
    }

    public void SaveEmbedding(int itemId, string sourceHash, string modelVersion, IReadOnlyList<float> embedding)
    {
        ItemEmbeddingDao? existing = GetEmbedding(itemId);
        if (existing is null)
        {
            existing = new ItemEmbeddingDao { ItemForeignKey = itemId };
            _dbContext.ItemEmbeddings.Add(existing);
        }

        existing.SourceHash = sourceHash;
        existing.ModelVersion = modelVersion;
        existing.Dimensions = embedding.Count;
        existing.Embedding = ToBytes(embedding);
        existing.ComputedAtUtc = DateTime.UtcNow;
    }

    public IReadOnlyList<LabelledEmbedding> GetLabelledEmbeddings()
    {
        var rows = _dbContext.ItemEmbeddings
            .Select(embedding => new
            {
                embedding.ItemForeignKey,
                embedding.Embedding,
                Tags = embedding.Item.Tags.Select(tag => tag.Name).ToList(),
            })
            .ToList();

        return rows
            .Where(row => row.Tags.Count > 0 && row.Embedding.Length > 0)
            .Select(row => new LabelledEmbedding(
                row.ItemForeignKey,
                ToFloats(row.Embedding),
                row.Tags))
            .ToList();
    }

    public IReadOnlyList<int> GetItemIdsWithPendingSuggestions()
    {
        return _dbContext.TagSuggestions
            .Where(suggestion => suggestion.State == TagSuggestionState.Pending)
            .Select(suggestion => suggestion.ItemForeignKey)
            .Distinct()
            .ToList();
    }

    public IReadOnlyList<string> GetItemNamesWithPendingSuggestions()
    {
        return _dbContext.TagSuggestions
            .Where(suggestion => suggestion.State == TagSuggestionState.Pending)
            .Select(suggestion => suggestion.Item.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToList();
    }

    public IReadOnlySet<int> GetItemIdsWithTags()
    {
        return _dbContext.ItemTags
            .Select(link => link.ItemForeignKey)
            .Distinct()
            .ToHashSet();
    }

    internal static byte[] ToBytes(IReadOnlyList<float> values)
    {
        byte[] bytes = new byte[values.Count * sizeof(float)];
        for (int i = 0; i < values.Count; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float)), values[i]);
        }

        return bytes;
    }

    internal static float[] ToFloats(byte[] bytes)
    {
        float[] values = new float[bytes.Length / sizeof(float)];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * sizeof(float)));
        }

        return values;
    }
}

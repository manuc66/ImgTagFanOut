using System.Collections.Generic;

namespace ImgTagFanOut.Dao;

public interface ISuggestionRepository
{
    /// <summary>Every stored suggestion for one image, all states included.</summary>
    IReadOnlyList<TagSuggestionDao> GetSuggestions(string itemName);

    /// <summary>Tag names the user already rejected for this image.</summary>
    IReadOnlyList<string> GetRejectedTags(string itemName);

    /// <summary>
    /// Replaces the pending suggestions of an image. Accepted and rejected rows
    /// are left alone: they are the feedback history.
    /// </summary>
    void ReplacePending(int itemId, string sourceHash, string engineVersion, IReadOnlyList<TagSuggestionRecord> suggestions);

    /// <summary>Accepts or rejects one suggestion. Unknown rows are ignored.</summary>
    void SetState(string itemName, string tagName, TagSuggestionState state);

    ItemEmbeddingDao? GetEmbedding(int itemId);

    void SaveEmbedding(int itemId, string sourceHash, string modelVersion, IReadOnlyList<float> embedding);

    /// <summary>Embeddings of every tagged image, the gallery the engine compares against.</summary>
    IReadOnlyList<LabelledEmbedding> GetLabelledEmbeddings();

    /// <summary>Images that still have at least one pending suggestion.</summary>
    IReadOnlyList<int> GetItemIdsWithPendingSuggestions();

    /// <summary>Names of the images with a pending suggestion, for the review command.</summary>
    IReadOnlyList<string> GetItemNamesWithPendingSuggestions();

    /// <summary>Images that already carry at least one tag, the useful half of the gallery.</summary>
    IReadOnlySet<int> GetItemIdsWithTags();
}

/// <summary>One suggestion as the engine produces it, before persistence.</summary>
public readonly record struct TagSuggestionRecord(string TagName, double Score, string Source, string ReasonArgument);

/// <summary>An image descriptor with the tag names attached to that image.</summary>
public sealed record LabelledEmbedding(int ItemId, float[] Embedding, IReadOnlyList<string> Tags);

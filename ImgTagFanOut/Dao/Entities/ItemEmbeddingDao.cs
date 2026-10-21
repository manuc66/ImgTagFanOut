using System;
using System.Diagnostics;

namespace ImgTagFanOut.Dao;

/// <summary>
/// A tag the local suggestion engine proposed for an image. Keeping rejected
/// suggestions is what stops the next scan from proposing them again.
/// </summary>
[DebuggerDisplay("{TagName} {Score} {State}")]
public class TagSuggestionDao
{
    public int ItemForeignKey { get; set; }
    public ItemDao Item { get; set; } = null!;

    /// <summary>
    /// Tag name, not a foreign key: a suggestion can be rejected before the tag
    /// exists, and a renamed tag must not silently keep old suggestions.
    /// </summary>
    public string TagName { get; set; } = "";

    /// <summary>Score between 0 and 1. Not a probability.</summary>
    public double Score { get; set; }

    /// <summary>Signal that produced the best score: path, face or similar.</summary>
    public string Source { get; set; } = "";

    /// <summary>Value the reason template needs (tag fragment, person name, count).</summary>
    public string ReasonArgument { get; set; } = "";

    /// <summary>Hash of the file when the suggestion was computed.</summary>
    public string SourceHash { get; set; } = "";

    public string EngineVersion { get; set; } = "";

    public TagSuggestionState State { get; set; }

    public DateTime SuggestedAtUtc { get; set; }
}

public enum TagSuggestionState
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
}

/// <summary>
/// Visual descriptor of one image, cached by content hash like the face
/// analysis. Computing it requires decoding the photo, so it is worth keeping.
/// </summary>
[DebuggerDisplay("{ItemForeignKey} {ModelVersion} {Dimensions}")]
public class ItemEmbeddingDao
{
    public int ItemForeignKey { get; set; }
    public ItemDao Item { get; set; } = null!;

    /// <summary>Hash of the file at analysis time: if it changes, the vector is redone.</summary>
    public string SourceHash { get; set; } = "";

    public string ModelVersion { get; set; } = "";

    public int Dimensions { get; set; }

    /// <summary>Little-endian float32 values, L2 normalised.</summary>
    public byte[] Embedding { get; set; } = [];

    public DateTime ComputedAtUtc { get; set; }
}

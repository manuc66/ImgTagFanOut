using System.Collections.Generic;
using System.Diagnostics;

namespace ImgTagFanOut.Dao;

/// <summary>
/// A face detected in an image, together with its cached embedding.
/// The two version columns are cache keys: when one changes, only the
/// matching part is recomputed.
/// </summary>
[DebuggerDisplay("{FaceId}#{ItemForeignKey} pos={Position}")]
public class FaceDao
{
    public int FaceId { get; set; }

    public int ItemForeignKey { get; set; }
    public ItemDao Item { get; set; } = null!;

    /// <summary>Left to right order in the image, stable for display.</summary>
    public int Position { get; set; }

    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }

    /// <summary>5 points (x, y) as float32, 40 bytes: enough to realign without storing an image.</summary>
    public byte[] Landmarks { get; set; } = [];

    public float Score { get; set; }

    /// <summary>L2 normalized embedding (128 × float32 for SFace), null until computed.</summary>
    public byte[]? Embedding { get; set; }

    /// <summary>Version of the embedder that produced <see cref="Embedding"/>.</summary>
    public string? EmbedderVersion { get; set; }
}

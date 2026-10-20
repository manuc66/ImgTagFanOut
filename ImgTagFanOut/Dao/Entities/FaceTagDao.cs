using System.Diagnostics;

namespace ImgTagFanOut.Dao;

/// <summary>
/// Links a detected face to the tag naming that person.
/// A tag with at least one link is what the UI calls a "person".
/// </summary>
[DebuggerDisplay("{FaceId}-{TagId}")]
public class FaceTagDao
{
    public int FaceId { get; set; }
    public FaceDao Face { get; set; } = null!;

    public int TagId { get; set; }
    public TagDao Tag { get; set; } = null!;
}

using System;
using System.Diagnostics;

namespace ImgTagFanOut.Dao;

/// <summary>
/// Analysis record for one image, also written when no face was found.
/// Without it, a landscape picture would be analyzed again on every pass.
/// </summary>
[DebuggerDisplay("{ItemForeignKey} {DetectorVersion} {FaceCount}")]
public class FaceAnalysisDao
{
    public int ItemForeignKey { get; set; }
    public ItemDao Item { get; set; } = null!;

    /// <summary>Hash of the file at analysis time: if the file changes, everything is redone.</summary>
    public string SourceHash { get; set; } = "";

    public string DetectorVersion { get; set; } = "";
    public int FaceCount { get; set; }
    public DateTime AnalyzedAtUtc { get; set; }
}

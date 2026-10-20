using System.Collections.Generic;
using ImgTagFanOut.Models.Faces;

namespace ImgTagFanOut.Dao;

public interface IFaceRepository
{
    FaceAnalysisDao? GetAnalysis(int itemId);

    IReadOnlyList<FaceDao> GetFaces(int itemId);

    /// <summary>
    /// Replaces the faces of an item and updates its analysis record.
    /// The item must already be persisted (ItemId assigned).
    /// </summary>
    void Replace(
        ItemDao item,
        string sourceHash,
        string detectorVersion,
        IReadOnlyList<AnalyzedFace> faces,
        string embedderVersion);
}

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

    /// <summary>Tags naming at least one detected face, sorted by name.</summary>
    IReadOnlyList<TagDao> GetPersonTags();

    /// <summary>Tag naming this face, or null when the face is still unknown.</summary>
    TagDao? GetPersonTag(int faceId);

    /// <summary>Names a face, replacing any previous link. Null clears the name.</summary>
    void SetPersonTag(FaceDao face, TagDao? tag);

    FaceDao? GetFace(int faceId);

    /// <summary>Faces of one image, with the person tag when it is already known.</summary>
    IReadOnlyList<FaceWithPerson> GetFacesWithPerson(string itemName);
}

/// <param name="Face">The detected face.</param>
/// <param name="PersonName">Name of the person, or null when the face is unknown.</param>
public readonly record struct FaceWithPerson(FaceDao Face, string? PersonName);

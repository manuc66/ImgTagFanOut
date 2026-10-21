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

    /// <summary>How many faces each person has taught, by tag name.</summary>
    IReadOnlyDictionary<string, int> GetPersonFaceCounts();

    /// <summary>Tag naming this face, or null when the face is still unknown.</summary>
    TagDao? GetPersonTag(int faceId);

    /// <summary>Names a face, replacing any previous link. Null clears the name.</summary>
    void SetPersonTag(FaceDao face, TagDao? tag);

    /// <summary>
    /// Drops every link of that person inside one image. Removing the label from the
    /// photo means the photo no longer claims that person, faces included.
    /// </summary>
    void ClearPersonLinks(string itemName, string personName);

    /// <summary>Drops every person link of one image, used when all labels are removed.</summary>
    void ClearAllPersonLinks(string itemName);

    FaceDao? GetFace(int faceId);

    /// <summary>Faces of one image, with the person tag when it is already known.</summary>
    IReadOnlyList<FaceWithPerson> GetFacesWithPerson(string itemName);

    /// <summary>Embeddings of every named face, used to recognise the others.</summary>
    List<PersonEmbedding> GetPersonEmbeddings();

    /// <summary>Faces nobody has named yet, with their photo, oldest first.</summary>
    List<FaceEmbedding> GetUnnamedFaces();
}

/// <param name="PersonName">Tag naming the person.</param>
/// <param name="Embedding">L2 normalized embedding of one of their faces.</param>
public sealed record PersonEmbedding(string PersonName, byte[] Embedding);

/// <param name="Face">The detected face.</param>
/// <param name="PersonName">Name of the person, or null when the face is unknown.</param>
public readonly record struct FaceWithPerson(FaceDao Face, string? PersonName);

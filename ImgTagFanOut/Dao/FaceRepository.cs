using System;
using System.Collections.Generic;
using System.Linq;
using ImgTagFanOut.Models.Faces;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Dao;

public class FaceRepository : IFaceRepository
{
    private readonly IImgTagFanOutDbContext _dbContext;

    public FaceRepository(IImgTagFanOutDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public FaceAnalysisDao? GetAnalysis(int itemId)
    {
        return _dbContext.FaceAnalyses.FirstOrDefault(x => x.ItemForeignKey == itemId);
    }

    public IReadOnlyList<FaceDao> GetFaces(int itemId)
    {
        return _dbContext.Faces
            .Where(x => x.ItemForeignKey == itemId)
            .OrderBy(x => x.Position)
            .ToList();
    }

    public void Replace(
        ItemDao item,
        string sourceHash,
        string detectorVersion,
        IReadOnlyList<AnalyzedFace> faces,
        string embedderVersion)
    {
        List<FaceDao> existing = _dbContext.Faces
            .Where(x => x.ItemForeignKey == item.ItemId)
            .ToList();

        if (existing.Count > 0)
        {
            // Person links go away with the face, without relying on SQLite's
            // ON DELETE CASCADE (only active when foreign keys are enforced).
            List<FaceTagDao> links = _dbContext.FaceTags
                .Where(link => link.Face.ItemForeignKey == item.ItemId)
                .ToList();
            if (links.Count > 0)
            {
                _dbContext.FaceTags.RemoveRange(links);
            }

            _dbContext.Faces.RemoveRange(existing);
        }

        for (int i = 0; i < faces.Count; i++)
        {
            AnalyzedFace face = faces[i];
            _dbContext.Faces.Add(new FaceDao
            {
                ItemForeignKey = item.ItemId,
                Position = i,
                X = face.Detection.X,
                Y = face.Detection.Y,
                Width = face.Detection.Width,
                Height = face.Detection.Height,
                Landmarks = FaceLandmarks.Encode(face.Detection.Landmarks),
                Score = face.Detection.Score,
                Embedding = face.Embedding,
                EmbedderVersion = face.Embedding is null ? null : embedderVersion,
            });
        }

        FaceAnalysisDao? analysis = GetAnalysis(item.ItemId);
        if (analysis is null)
        {
            analysis = new FaceAnalysisDao { ItemForeignKey = item.ItemId };
            _dbContext.FaceAnalyses.Add(analysis);
        }

        analysis.SourceHash = sourceHash;
        analysis.DetectorVersion = detectorVersion;
        analysis.FaceCount = faces.Count;
        analysis.AnalyzedAtUtc = DateTime.UtcNow;
    }

    public IReadOnlyList<TagDao> GetPersonTags()
    {
        return _dbContext.FaceTags
            .Select(link => link.Tag)
            .Distinct()
            .OrderBy(tag => tag.Name)
            .ToList();
    }

    public IReadOnlyDictionary<string, int> GetPersonFaceCounts()
    {
        return _dbContext.FaceTags
            .GroupBy(link => link.Tag.Name)
            .Select(group => new { Name = group.Key, Count = group.Count() })
            .ToDictionary(row => row.Name, row => row.Count);
    }

    public TagDao? GetPersonTag(int faceId)
    {
        return _dbContext.FaceTags
            .Where(link => link.FaceId == faceId)
            .Select(link => link.Tag)
            .FirstOrDefault();
    }

    public FaceDao? GetFace(int faceId)
    {
        return _dbContext.Faces.FirstOrDefault(face => face.FaceId == faceId);
    }

    public IReadOnlyList<FaceWithPerson> GetFacesWithPerson(string itemName)
    {
        return _dbContext.Faces
            .Where(face => face.Item.Name == itemName)
            .OrderBy(face => face.Position)
            .Select(face => new FaceWithPerson(
                face,
                face.FaceTags.Select(link => link.Tag.Name).FirstOrDefault()))
            .ToList();
    }

    public List<FaceEmbedding> GetUnnamedFaces()
    {
        return _dbContext.Faces
            .Where(face => face.Embedding != null && !face.FaceTags.Any())
            .OrderBy(face => face.FaceId)
            .Select(face => new { face.FaceId, face.Item.Name, face.X, face.Y, face.Width, face.Height, face.Embedding })
            .ToList()
            .Select(row => new FaceEmbedding(row.FaceId, row.Name, row.X, row.Y, row.Width, row.Height, row.Embedding!))
            .ToList();
    }

    public List<PersonEmbedding> GetPersonEmbeddings()
    {
        var rows = _dbContext.FaceTags
            .Where(link => link.Face.Embedding != null)
            .Select(link => new { link.Tag.Name, link.Face.Embedding })
            .ToList();

        return rows
            .Select(row => new PersonEmbedding(row.Name, row.Embedding!))
            .ToList();
    }

    public void ClearPersonLinks(string itemName, string personName)
    {
        List<FaceTagDao> links = _dbContext.FaceTags
            .Where(link => link.Face.Item.Name == itemName && link.Tag.Name == personName)
            .ToList();

        if (links.Count > 0)
        {
            _dbContext.FaceTags.RemoveRange(links);
        }
    }

    public void ClearAllPersonLinks(string itemName)
    {
        List<FaceTagDao> links = _dbContext.FaceTags
            .Where(link => link.Face.Item.Name == itemName)
            .ToList();

        if (links.Count > 0)
        {
            _dbContext.FaceTags.RemoveRange(links);
        }
    }

    public void SetPersonTag(FaceDao face, TagDao? tag)
    {
        List<FaceTagDao> existing = _dbContext.FaceTags
            .Where(link => link.FaceId == face.FaceId)
            .ToList();

        if (existing.Count > 0)
        {
            _dbContext.FaceTags.RemoveRange(existing);
        }

        if (tag is not null)
        {
            _dbContext.FaceTags.Add(new FaceTagDao
            {
                FaceId = face.FaceId,
                Face = face,
                TagId = tag.TagId,
                Tag = tag,
            });
        }
    }
}

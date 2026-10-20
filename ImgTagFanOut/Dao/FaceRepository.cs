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
}

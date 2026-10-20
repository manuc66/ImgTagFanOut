using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImgTagFanOut.Dao;

namespace ImgTagFanOut.Models.Faces;

/// <summary>
/// Runs the analysis of one image, recomputing only what changed.
/// Detections and embeddings are invalidated separately: bumping the embedder
/// does not re-run detection, and the other way round.
/// </summary>
public sealed class FaceAnalysisService
{
    private readonly IFaceAnalyzer _analyzer;

    public FaceAnalysisService(IFaceAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    public async Task<FaceAnalysisOutcome> AnalyzeAsync(
        IUnitOfWork unitOfWork,
        ItemDao item,
        string sourceHash,
        string fullFilePath,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FaceDao> cached = unitOfWork.FaceRepository.GetFaces(item.ItemId);
        FaceAnalysisDao? analysis = unitOfWork.FaceRepository.GetAnalysis(item.ItemId);

        bool detectionsReusable = IsDetectionCacheValid(analysis, cached, sourceHash);
        bool embeddingsReusable = detectionsReusable && HaveCurrentEmbeddings(cached);

        if (embeddingsReusable)
        {
            return new FaceAnalysisOutcome(cached.Count, DetectedFromCache: true, EmbeddedFromCache: true);
        }

        IReadOnlyList<FaceDetection>? knownDetections = detectionsReusable
            ? cached.Select(ToDetection).ToArray()
            : null;

        FaceAnalysisResult result = await _analyzer.AnalyzeAsync(
            fullFilePath,
            knownDetections,
            computeEmbeddings: true,
            cancellationToken);

        unitOfWork.FaceRepository.Replace(
            item,
            sourceHash,
            _analyzer.DetectorVersion,
            result.Faces,
            _analyzer.EmbedderVersion);

        return new FaceAnalysisOutcome(
            result.Faces.Count,
            DetectedFromCache: detectionsReusable,
            EmbeddedFromCache: false);
    }

    private bool IsDetectionCacheValid(FaceAnalysisDao? analysis, IReadOnlyList<FaceDao> cached, string sourceHash)
    {
        return analysis is not null
               && !string.IsNullOrEmpty(sourceHash)
               && analysis.SourceHash == sourceHash
               && analysis.DetectorVersion == _analyzer.DetectorVersion
               && analysis.FaceCount == cached.Count;
    }

    private bool HaveCurrentEmbeddings(IReadOnlyList<FaceDao> cached)
    {
        return cached.All(f => f.Embedding is { Length: > 0 } && f.EmbedderVersion == _analyzer.EmbedderVersion);
    }

    private static FaceDetection ToDetection(FaceDao face)
    {
        return new FaceDetection(
            face.X,
            face.Y,
            face.Width,
            face.Height,
            face.Score,
            FaceLandmarks.Decode(face.Landmarks));
    }
}

/// <param name="FaceCount">Number of faces in the image.</param>
/// <param name="DetectedFromCache">True when detection was not re-run.</param>
/// <param name="EmbeddedFromCache">True when nothing at all was recomputed.</param>
public readonly record struct FaceAnalysisOutcome(int FaceCount, bool DetectedFromCache, bool EmbeddedFromCache);

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ImgTagFanOut.Models.Faces;

/// <summary>
/// Face detection and embedding for one image.
/// The real implementation uses OpenCV (YuNet + SFace); tests use a fake so
/// the cache logic can be verified without any model.
/// </summary>
public interface IFaceAnalyzer : IDisposable
{
    /// <summary>
    /// Fingerprint of the detector and its settings (model, working resolution,
    /// thresholds). Changing it invalidates every cached detection.
    /// </summary>
    string DetectorVersion { get; }

    /// <summary>
    /// Fingerprint of the embedding model and its preprocessing. Changing it
    /// invalidates embeddings only, detections are kept.
    /// </summary>
    string EmbedderVersion { get; }

    /// <summary>
    /// Analyzes one image.
    /// </summary>
    /// <param name="fullFilePath">Full path of the image.</param>
    /// <param name="knownDetections">
    /// Already cached detections, to skip detection; <c>null</c> to detect.
    /// </param>
    /// <param name="computeEmbeddings">Compute embeddings when detections are reused.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<FaceAnalysisResult> AnalyzeAsync(
        string fullFilePath,
        IReadOnlyList<FaceDetection>? knownDetections,
        bool computeEmbeddings,
        CancellationToken cancellationToken);
}

public sealed record FaceAnalysisResult(IReadOnlyList<AnalyzedFace> Faces);

/// <param name="Detection">Face geometry.</param>
/// <param name="Embedding">L2 normalized embedding, or <c>null</c> when not requested.</param>
public sealed record AnalyzedFace(FaceDetection Detection, byte[]? Embedding);

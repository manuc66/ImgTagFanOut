using System;
using System.Threading;
using System.Threading.Tasks;

namespace ImgTagFanOut.Models.Faces;

/// <summary>
/// Runs the face analysis over a working folder.
/// </summary>
public interface IFaceScan
{
    Task<FaceScanSummary> ScanAsync(
        string workingFolder,
        IProgress<FaceScanProgress>? progress,
        CancellationToken cancellationToken);
}

/// <param name="Processed">Images visited so far, including skipped ones.</param>
/// <param name="Total">Images known in the workspace.</param>
/// <param name="FacesFound">Faces found so far, cache included.</param>
public readonly record struct FaceScanProgress(int Processed, int Total, int FacesFound);

/// <param name="Total">Images known in the workspace.</param>
/// <param name="Analyzed">Images whose analysis is available.</param>
/// <param name="Cached">Images that needed no recomputation at all.</param>
/// <param name="Skipped">Images whose file is gone.</param>
/// <param name="Failures">Images that could not be read or analyzed.</param>
/// <param name="AnalyzerUnavailable">True when the models or the native library are missing.</param>
public readonly record struct FaceScanSummary(
    int Total,
    int Analyzed,
    int Cached,
    int FaceCount,
    int Skipped,
    int Failures,
    bool AnalyzerUnavailable,
    TimeSpan Duration);

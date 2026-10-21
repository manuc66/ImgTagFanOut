using System;
using System.Threading;
using System.Threading.Tasks;

namespace ImgTagFanOut.Models.Suggestions;

public interface ITagSuggestionScan
{
    Task<TagSuggestionScanSummary> ScanAsync(
        string workingFolder,
        IProgress<TagSuggestionScanProgress>? progress,
        CancellationToken cancellationToken);
}

public readonly record struct TagSuggestionScanProgress(int Processed, int Total, int Suggested);

/// <param name="EmbedderUnavailable">
/// The local vision model could not be loaded. Path and face signals still ran.
/// </param>
public sealed record TagSuggestionScanSummary(
    int Total,
    int SuggestedItems,
    int SuggestionCount,
    int ComputedEmbeddings,
    int CachedEmbeddings,
    int Skipped,
    int Failures,
    bool EmbedderUnavailable,
    TimeSpan Duration);

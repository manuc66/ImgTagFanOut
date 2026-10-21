using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImgTagFanOut.Dao;
using Serilog;

namespace ImgTagFanOut.Models.Suggestions;

/// <summary>
/// Walks the library, computes an image descriptor once per content hash, and
/// stores the tags the local engine proposes. It never assigns a tag: the user
/// accepts or rejects every suggestion.
/// </summary>
public sealed class TagSuggestionScan : ITagSuggestionScan
{
    /// <summary>Images between two saves: an interrupted run keeps its work.</summary>
    private const int SaveEvery = 50;

    /// <summary>After this many consecutive model failures, stop decoding images and keep the rules.</summary>
    private const int MaxConsecutiveEmbedFailures = 3;

    /// <summary>
    /// Bound on the comparison gallery. Cosine is linear in the gallery size,
    /// so a ten-thousand-photo album would otherwise make every image pay for
    /// the whole library. The most recently added descriptors are kept.
    /// </summary>
    private const int MaxGallerySize = 2000;

    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IHashEvaluator _hashEvaluator;
    private readonly Func<IImageEmbedder> _embedderFactory;
    private readonly TagSuggestionEngine _engine;

    public TagSuggestionScan(IUnitOfWorkFactory unitOfWorkFactory, IHashEvaluator hashEvaluator)
        : this(unitOfWorkFactory, hashEvaluator, static () => new OnnxImageEmbedder(), new TagSuggestionEngine()) { }

    internal TagSuggestionScan(
        IUnitOfWorkFactory unitOfWorkFactory,
        IHashEvaluator hashEvaluator,
        Func<IImageEmbedder> embedderFactory,
        TagSuggestionEngine engine)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _hashEvaluator = hashEvaluator;
        _embedderFactory = embedderFactory;
        _engine = engine;
    }

    public async Task<TagSuggestionScanSummary> ScanAsync(
        string workingFolder,
        IProgress<TagSuggestionScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        IImageEmbedder? embedder;
        try
        {
            embedder = _embedderFactory();
        }
        catch (Exception exception)
        {
            // A missing model must not disable the path and face signals.
            Log.Warning(exception, "Local image embedding model unavailable, suggestions will use metadata only");
            embedder = null;
        }

        using (embedder)
        {
            IImageEmbedder? active = embedder;
            await using IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(workingFolder, cancellationToken);

            IReadOnlyList<ItemDao> items = unitOfWork.TagRepository.GetAllItems();
            IReadOnlyList<string> vocabulary = unitOfWork.TagRepository.GetAllTag().Select(tag => tag.Name).ToList();
            List<LabelledEmbedding> gallery = unitOfWork.SuggestionRepository.GetLabelledEmbeddings().ToList();
            if (gallery.Count > MaxGallerySize)
            {
                gallery = gallery.Skip(gallery.Count - MaxGallerySize).ToList();
            }

            // Tagged photos first: their descriptors build the gallery the
            // untagged ones are compared against, so the result must not depend
            // on the alphabetical order of file names.
            IReadOnlySet<int> taggedItemIds = unitOfWork.SuggestionRepository.GetItemIdsWithTags();
            items = items
                .OrderBy(item => taggedItemIds.Contains(item.ItemId) ? 0 : 1)
                .ThenBy(item => item.Name, StringComparer.Ordinal)
                .ToList();

            string engineVersion = active is null ? "rules/v1" : $"rules/v1+{active.Version}";

            int suggestedItems = 0;
            int suggestionCount = 0;
            int computed = 0;
            int cached = 0;
            int skipped = 0;
            int failures = 0;
            int processed = 0;
            int pendingSave = 0;
            int consecutiveEmbedFailures = 0;
            bool embedderUnavailable = active is null;

            try
            {
                foreach (ItemDao item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    processed++;

                    string fullPath = Path.Combine(workingFolder, item.Name);
                    if (!File.Exists(fullPath))
                    {
                        skipped++;
                        progress?.Report(new TagSuggestionScanProgress(processed, items.Count, suggestionCount));
                        continue;
                    }

                    try
                    {
                        string hash = await EnsureHashAsync(item, fullPath, cancellationToken);

                        float[]? embedding = null;
                        if (active is not null)
                        {
                            ItemEmbeddingDao? stored = unitOfWork.SuggestionRepository.GetEmbedding(item.ItemId);
                            if (IsUsable(stored, hash, active.Version))
                            {
                                embedding = SuggestionRepository.ToFloats(stored!.Embedding);
                                cached++;
                            }
                            else
                            {
                                try
                                {
                                    embedding = active.Embed(fullPath);
                                    unitOfWork.SuggestionRepository.SaveEmbedding(item.ItemId, hash, active.Version, embedding);
                                    computed++;
                                    consecutiveEmbedFailures = 0;
                                }
                                catch (Exception exception) when (exception is not OperationCanceledException)
                                {
                                    // An unsupported format or a truncated file must
                                    // not abort the batch, nor keep every photo waiting
                                    // on a model that is broken.
                                    failures++;
                                    consecutiveEmbedFailures++;
                                    Log.Warning(exception, "Cannot compute the image descriptor of {File}", fullPath);

                                    if (consecutiveEmbedFailures >= MaxConsecutiveEmbedFailures)
                                    {
                                        Log.Warning("Disabling the image embedding model after repeated failures");
                                        active = null;
                                        embedderUnavailable = true;
                                    }
                                }
                            }
                        }

                        IReadOnlyList<string> assigned = unitOfWork.TagRepository
                            .GetTagNamesForItem(item.ItemId);
                        IReadOnlyList<string> personNames = unitOfWork.FaceRepository
                            .GetFacesWithPerson(item.Name)
                            .Select(row => row.PersonName)
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Select(name => name!)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        HashSet<string> rejected = unitOfWork.SuggestionRepository
                            .GetRejectedTags(item.Name)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

                        IReadOnlyList<TagSuggestionRecord> suggestions = _engine.Suggest(
                            item.Name,
                            item.ItemId,
                            assigned.ToHashSet(StringComparer.OrdinalIgnoreCase),
                            personNames,
                            embedding,
                            vocabulary,
                            gallery,
                            rejected);

                        unitOfWork.SuggestionRepository.ReplacePending(item.ItemId, hash, engineVersion, suggestions);
                        pendingSave++;

                        if (suggestions.Count > 0)
                        {
                            suggestedItems++;
                            suggestionCount += suggestions.Count;
                        }

                        // A tagged photo with a fresh descriptor is immediately
                        // useful to the next images of the same run.
                        if (embedding is not null && assigned.Count > 0 && gallery.Count < MaxGallerySize)
                        {
                            gallery.Add(new LabelledEmbedding(item.ItemId, embedding, assigned));
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        failures++;
                        Log.Warning(exception, "Tag suggestion failed for {File}", fullPath);
                    }

                    if (pendingSave >= SaveEvery)
                    {
                        await unitOfWork.SaveChangesAsync(cancellationToken);
                        pendingSave = 0;
                    }

                    progress?.Report(new TagSuggestionScanProgress(processed, items.Count, suggestionCount));
                }
            }
            finally
            {
                // Whatever was computed must survive a cancellation.
                await unitOfWork.SaveChangesAsync(CancellationToken.None);

                Log.Information(
                    "Tag suggestions: {Suggested} images with {Count} suggestions, {Computed} descriptors computed, {Cached} cached, {Skipped} missing files, {Failures} failures in {Duration}",
                    suggestedItems, suggestionCount, computed, cached, skipped, failures, stopwatch.Elapsed);
            }

            return new TagSuggestionScanSummary(
                items.Count,
                suggestedItems,
                suggestionCount,
                computed,
                cached,
                skipped,
                failures,
                embedderUnavailable,
                stopwatch.Elapsed);
        }
    }

    private static bool IsUsable(ItemEmbeddingDao? stored, string hash, string version)
    {
        return stored is not null
               && stored.SourceHash == hash
               && stored.ModelVersion == version
               && stored.Dimensions > 0
               && stored.Embedding.Length == stored.Dimensions * sizeof(float);
    }

    private async Task<string> EnsureHashAsync(ItemDao item, string fullPath, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(item.Hash))
        {
            return item.Hash;
        }

        string hash = await _hashEvaluator.ComputeHashAsync(fullPath, cancellationToken);
        item.Hash = hash;
        return hash;
    }
}

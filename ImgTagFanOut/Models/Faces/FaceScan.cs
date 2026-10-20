using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImgTagFanOut.Dao;
using ImgTagFanOut.ViewModels;
using Serilog;

namespace ImgTagFanOut.Models.Faces;

/// <summary>
/// Walks the workspace and feeds every image through <see cref="FaceAnalysisService"/>.
/// Already analyzed images cost two indexed SQLite reads, nothing more.
/// </summary>
public sealed class FaceScan : IFaceScan
{
    /// <summary>
    /// Images analyzed between two SaveChanges. Low enough to keep an interrupted
    /// run useful, high enough not to commit per image.
    /// </summary>
    private const int SaveEvery = 50;

    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IHashEvaluator _hashEvaluator;
    private readonly Func<IFaceAnalyzer> _analyzerFactory;

    public FaceScan(IUnitOfWorkFactory unitOfWorkFactory, IHashEvaluator hashEvaluator)
        : this(
            unitOfWorkFactory,
            hashEvaluator,
            static () => new OpenCvFaceAnalyzer(OpenCvFaceAnalyzer.DefaultModelsDirectory))
    {
    }

    internal FaceScan(
        IUnitOfWorkFactory unitOfWorkFactory,
        IHashEvaluator hashEvaluator,
        Func<IFaceAnalyzer> analyzerFactory)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _hashEvaluator = hashEvaluator;
        _analyzerFactory = analyzerFactory;
    }

    public async Task<FaceScanSummary> ScanAsync(
        string workingFolder,
        IProgress<FaceScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        IFaceAnalyzer analyzer;
        try
        {
            analyzer = _analyzerFactory();
        }
        catch (Exception e)
        {
            // Modèles absents, natifs OpenCV indisponibles (macOS), etc.
            Log.Warning(e, "Analyse des visages indisponible");
            return new FaceScanSummary(0, 0, 0, 0, 0, 0, AnalyzerUnavailable: true, stopwatch.Elapsed);
        }

        using (analyzer)
        {
            await using IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(workingFolder, cancellationToken);

            ImmutableList<ItemDao> items = unitOfWork.TagRepository.GetAllItems();
            FaceAnalysisService service = new(analyzer);

            int analyzed = 0;
            int cached = 0;
            int skipped = 0;
            int failures = 0;
            int faceCount = 0;
            int processed = 0;
            int pendingSave = 0;

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
                        progress?.Report(new FaceScanProgress(processed, items.Count, faceCount));
                        continue;
                    }

                    try
                    {
                        string hash = await EnsureHashAsync(item, fullPath, cancellationToken);

                        FaceAnalysisOutcome outcome = await service.AnalyzeAsync(
                            unitOfWork, item, hash, fullPath, cancellationToken);

                        faceCount += outcome.FaceCount;
                        analyzed++;

                        if (outcome.EmbeddedFromCache)
                        {
                            cached++;
                        }
                        else
                        {
                            pendingSave++;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        // Une image illisible ne doit pas arrêter le lot.
                        failures++;
                        Log.Warning(e, "Analyse de visage impossible pour {File}", fullPath);
                    }

                    if (pendingSave >= SaveEvery)
                    {
                        await unitOfWork.SaveChangesAsync(cancellationToken);
                        pendingSave = 0;
                    }

                    progress?.Report(new FaceScanProgress(processed, items.Count, faceCount));
                }
            }
            finally
            {
                // Ce qui a été calculé doit survivre à une annulation.
                await unitOfWork.SaveChangesAsync(CancellationToken.None);

                Log.Information(
                    "Analyse des visages : {Analyzed} images, {Cached} déjà à jour, {Faces} visages, {Skipped} fichiers absents, {Failures} échecs en {Duration}",
                    analyzed, cached, faceCount, skipped, failures, stopwatch.Elapsed);
            }

            return new FaceScanSummary(
                items.Count,
                analyzed,
                cached,
                faceCount,
                skipped,
                failures,
                AnalyzerUnavailable: false,
                stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Le hash est la clé du cache : on le calcule une fois puis on le garde.
    /// </summary>
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

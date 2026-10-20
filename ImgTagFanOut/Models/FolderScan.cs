using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using ImgTagFanOut.Dao;
using ImgTagFanOut.ViewModels;
using Serilog;

namespace ImgTagFanOut.Models;

public class FolderScan : IFolderScan
{
    private readonly IHashEvaluator _hashEvaluator;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;

    public FolderScan() : this(new HashEvaluator(), new UnitOfWorkFactory()) { }

    internal FolderScan(IHashEvaluator hashEvaluator, IUnitOfWorkFactory unitOfWorkFactory)
    {
        _hashEvaluator = hashEvaluator;
        _unitOfWorkFactory = unitOfWorkFactory;
    }

    public async Task ScanFolder(CancellationToken cancellationToken, string workingFolder, SourceList<CanHaveTag> images)
    {
        if (string.IsNullOrWhiteSpace(workingFolder))
        {
            throw new ArgumentException("The library folder is required.", nameof(workingFolder));
        }

        if (!Directory.Exists(workingFolder))
        {
            throw new DirectoryNotFoundException($"The library folder does not exist: {workingFolder}");
        }

        HashSet<string> allowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".jpe",
            ".jif",
            ".jfif",
            ".jfi", // jpeg
            ".jp2",
            ".j2k",
            ".jpf",
            ".jpm",
            ".jpg2",
            ".j2c",
            ".jpc",
            ".jpx",
            ".mj2", //JPEG 2000
            ".png",
            ".gif",
            ".webp",
            ".bmp",
            ".dib",
            ".heif",
            ".heic",
            ".heics",
            ".avci",
            ".avcs",
            ".hif", //High Efficiency Image File Format
            ".tiff",
            ".tif", // TIFF
            ".cr2", // RAW
        };

        EnumerationOptions options = new()
        {
            RecurseSubdirectories = true,
            // A protected subfolder must not make the whole library unscannable.
            IgnoreInaccessible = true,
            // Do not follow folder symlinks: a link back to a parent would loop.
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        };

        IEnumerable<string> enumerateFiles = Directory
            .EnumerateFiles(workingFolder, "*", options)
            .Where(x => allowedExtensions.Contains(Path.GetExtension(x)))
            .OrderBy(x => x, StringComparer.Ordinal);

        await using IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(workingFolder, cancellationToken);
        List<CanHaveTag> allCanHaveTags = [];

        try
        {
            foreach (string file in enumerateFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                CanHaveTag canHaveTag = new(Path.GetRelativePath(workingFolder, file));
                allCanHaveTags.Add(canHaveTag);

                try
                {
                    await unitOfWork.TagRepository.AddOrUpdateItem(
                        canHaveTag,
                        tag => _hashEvaluator.ComputeHashAsync(tag.GetFullFilePath(workingFolder), cancellationToken));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    // A file deleted while scanning, or one we cannot read: keep
                    // it visible but leave its database row untouched.
                    Log.Warning(ex, "Cannot scan {File}", file);
                }
            }
        }
        finally
        {
            // Whatever was read before a cancellation stays in the database.
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        images.AddRange(allCanHaveTags);
    }
}

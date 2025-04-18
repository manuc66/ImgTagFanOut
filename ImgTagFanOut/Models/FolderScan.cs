using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using ImgTagFanOut.Dao;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Models;

public class FolderScan
{
    private readonly HashEvaluator _hashEvaluator = new();

    internal async Task ScanFolder(CancellationToken cancellationToken, string workingFolder, SourceList<CanHaveTag> images)
    {
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

        IEnumerable<string> enumerateFiles = Directory
            .EnumerateFiles(workingFolder, "*", SearchOption.AllDirectories)
            .Where(x => !cancellationToken.IsCancellationRequested && allowedExtensions.Contains(Path.GetExtension(x)));

        await using IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(workingFolder, cancellationToken);
        List<CanHaveTag> allCanHaveTags = [];
        foreach (string file in enumerateFiles)
        {
            CanHaveTag canHaveTag = new(Path.GetRelativePath(workingFolder, file));

            allCanHaveTags.Add(canHaveTag);

            await unitOfWork.TagRepository.AddOrUpdateItem(canHaveTag, tag => _hashEvaluator.ComputeHashAsync(tag.GetFullFilePath(workingFolder), cancellationToken));
        }

        images.AddRange(allCanHaveTags);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

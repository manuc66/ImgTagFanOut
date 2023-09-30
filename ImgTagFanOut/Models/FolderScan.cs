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
    internal async Task ScanFolder(CancellationToken cancellationToken, string workingFolder, SourceList<CanHaveTag> images)
    {
        HashSet<string> allowedExtensions = new() { ".jpeg", ".jpg", ".png", ".gif", ".webp", ".bmp" };

        IEnumerable<string> enumerateFiles = Directory.EnumerateFiles(workingFolder, "*", SearchOption.AllDirectories)
            .Where(x => allowedExtensions.Contains(Path.GetExtension(x)));


        await using IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(workingFolder);
        List<CanHaveTag> allCanHaveTags = new();
        foreach (string file in enumerateFiles)
        {
            CanHaveTag canHaveTag = new(Path.GetRelativePath(workingFolder, file));

            allCanHaveTags.Add(canHaveTag);

            unitOfWork.TagRepository.AddOrUpdateItem(canHaveTag);
        }
        images.AddRange(allCanHaveTags);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
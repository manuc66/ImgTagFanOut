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

        await using (IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(workingFolder))
        {
            IEnumerable<string> enumerateFiles = Directory.EnumerateFiles(workingFolder, "*", SearchOption.AllDirectories)
                .Where(x => allowedExtensions.Contains(Path.GetExtension(x)));

            foreach (string file in enumerateFiles)
            {
                CanHaveTag canHaveTag = new(Path.GetRelativePath(workingFolder, file));

                images.Add(canHaveTag);

                unitOfWork.TagRepository.AddOrUpdateItem(canHaveTag);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
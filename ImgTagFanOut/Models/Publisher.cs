using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImgTagFanOut.Dao;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Models;

public class Publisher
{
    public async Task PublishToFolder(CancellationToken cancellationToken, string workingFolder, string targetFolder)
    {
        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }

        await using (IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(workingFolder))
        {
            foreach (Tag tag in unitOfWork.TagRepository.GetAllTag())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                
                ImmutableList<string> itemsInDb = unitOfWork.TagRepository.GetItemsWithTag(tag);

                if (itemsInDb.Count == 0)
                {
                    continue;
                }

                string targetDirectoryForTag = Path.Combine(targetFolder, tag.Name);
                if (!Directory.Exists(targetDirectoryForTag))
                {
                    Directory.CreateDirectory(targetDirectoryForTag);
                }

                foreach (string itemInDb in itemsInDb)
                {
                    string itemFullPath = Path.Combine(workingFolder, itemInDb);

                    string fileName = Path.GetFileName(itemFullPath);

                    if (File.Exists(itemFullPath))
                    {
                        string destFileName = Path.Combine(targetDirectoryForTag, fileName);
                        if (!File.Exists(destFileName))
                        {
                           await CopyFileAsync(itemFullPath, destFileName, cancellationToken);
                        }
                        else
                        {
                            // todo: check if source and target are the same in this case do nothing otherwise rename the file
                        }
                    }
                    else
                    {
                        // skip, the source file is not found, this could be a warning!
                    }
                }
            }
        }
    }

    private static async Task CopyFileAsync(string sourceFile, string destinationFile, CancellationToken cancellationToken)
    {
        int bufferSize = 4096;
        await using (FileStream sourceStream = new(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
        await using (FileStream destinationStream = new(destinationFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
        
        File.SetLastWriteTime(destinationFile, File.GetLastWriteTime(sourceFile));
    }
}
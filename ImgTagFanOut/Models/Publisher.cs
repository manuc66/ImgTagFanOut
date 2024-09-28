using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models.CompareAlgorithms;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Models;

public class Publisher
{
    public async Task PublishToFolder(string workingFolder, string targetFolder, bool dropEverythingFirst,
        Action<Tag> beginTag, Action<(string source, string? destination, bool copied)> onFileCompleted,
        Action<(string path, bool sucess, string? error)> onFileDeleted,
        Action<(string path, bool sucess, string? error)> onDirectoryDeleted,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }
        else
        {
            if (dropEverythingFirst)
            {
                DeleteDirectoryRecursively(targetFolder, onFileDeleted, onDirectoryDeleted, cancellationToken);
            }
        }

        await using IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(workingFolder);

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

            await PublishTag(workingFolder, targetFolder, beginTag, onFileCompleted, tag, itemsInDb, cancellationToken);
        }
    }

    private static async Task PublishTag(string workingFolder, string targetFolder, Action<Tag> beginTag,
        Action<(string source, string? destination, bool copied)> onFileCompleted, Tag tag,
        ImmutableList<string> itemsInDb, CancellationToken cancellationToken)
    {
        beginTag(tag);

        string targetDirectoryForTag = Path.Combine(targetFolder, tag.Name);
        if (!Directory.Exists(targetDirectoryForTag))
        {
            Directory.CreateDirectory(targetDirectoryForTag);
        }

        foreach (string itemInDb in itemsInDb)
        {
            (string source, string? destination, bool copied) result =
                await PublishFile(cancellationToken, workingFolder, itemInDb, targetDirectoryForTag);
            onFileCompleted(result);
        }
    }

    private static async Task<(string source, string? destination, bool copied)> PublishFile(
        CancellationToken cancellationToken, string workingFolder, string itemInDb, string targetDirectoryForTag)
    {
        string itemFullPath = Path.Combine(workingFolder, itemInDb);

        string fileName = Path.GetFileName(itemFullPath);

        if (!File.Exists(itemFullPath))
        {
            // skip, the source file is not found, this could be a warning!
            return (itemFullPath, null, false);
        }

        string destFileName = Path.Combine(targetDirectoryForTag, fileName);
        if (File.Exists(destFileName))
        {
            FileInfo itemFullPathFi = new(itemFullPath);
            FileInfo destFileNameFi = new(destFileName);
            if (FileExt.FilesAreEqual(itemFullPathFi, destFileNameFi))
            {
                // skip, the source file is the same as the target
                return (itemFullPath, destFileName, false);
            }

            string fileExtension = Path.GetExtension(fileName);
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            int num = 0;
            do
            {
                num++;
                destFileName = Path.Combine(targetDirectoryForTag,
                    nameWithoutExtension + " (" + num + ")" + fileExtension);
                destFileNameFi = new(destFileName);
                if (destFileNameFi.Exists && FileExt.FilesAreEqual(itemFullPathFi, destFileNameFi))
                {
                    // skip, the source file is the same as the target, so already copied
                    return (itemFullPath, destFileName, false);
                }
            } while (File.Exists(destFileName));
        }

        await CopyFileAsync(itemFullPath, destFileName, cancellationToken);
        return (itemFullPath, destFileName, true);
    }

    private static async Task CopyFileAsync(string sourceFile, string destinationFile,
        CancellationToken cancellationToken)
    {
        int bufferSize = 4096;
        await using (FileStream sourceStream = new(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read,
                         bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
        await using (FileStream destinationStream = new(destinationFile, FileMode.CreateNew, FileAccess.Write,
                         FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);

        File.SetLastWriteTime(destinationFile, File.GetLastWriteTime(sourceFile));
    }

    static void DeleteDirectoryRecursively(string targetDir, Action<(string path, bool sucess, string? error)> onFileDeleted,
        Action<(string path, bool sucess, string? error)> onDirectoryDeleted,
        CancellationToken cancellationToken)
    {
        try
        {
            // Delete all files in the directory
            foreach (string file in Directory.GetFiles(targetDir))
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                try
                {
                    File.Delete(file);
                    onFileDeleted((file, true, null));
                }
                catch (Exception ex)
                {
                    onFileDeleted((file, false, ex.Message));
                }
            }
            
            if (cancellationToken.IsCancellationRequested) return;

            // Delete all subdirectories in the directory
            foreach (string subDir in Directory.GetDirectories(targetDir))
            {
                if (cancellationToken.IsCancellationRequested) break;
                DeleteDirectoryRecursively(subDir, onFileDeleted, onDirectoryDeleted, cancellationToken);
            }
            
            if (cancellationToken.IsCancellationRequested) return;

            // Delete the directory itself
            Directory.Delete(targetDir, false);
            onDirectoryDeleted((targetDir, true, null));
        }
        catch (Exception ex)
        {
            onDirectoryDeleted((targetDir, false, ex.Message));
        }
    }
}

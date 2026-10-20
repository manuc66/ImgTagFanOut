using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models.CompareAlgorithms;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Models;

public class Publisher : IPublisher
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;

    public Publisher() : this(new UnitOfWorkFactory()) { }

    internal Publisher(IUnitOfWorkFactory unitOfWorkFactory)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
    }

    public async Task PublishToFolder(
        string workingFolder,
        string targetFolder,
        bool dropEverythingFirst,
        PublishLinkMode linkMode,
        Action<Tag> beginTag,
        Action<PublishFileResult> onFileCompleted,
        Action<(string path, bool success, string? error)> onFileDeleted,
        Action<(string path, bool success, string? error)> onDirectoryDeleted,
        CancellationToken cancellationToken
    )
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

        await using IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(workingFolder, cancellationToken);

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

            await PublishTag(workingFolder, targetFolder, linkMode, beginTag, onFileCompleted, tag, itemsInDb, cancellationToken);
        }
    }

    private static async Task PublishTag(
        string workingFolder,
        string targetFolder,
        PublishLinkMode linkMode,
        Action<Tag> beginTag,
        Action<PublishFileResult> onFileCompleted,
        Tag tag,
        ImmutableList<string> itemsInDb,
        CancellationToken cancellationToken
    )
    {
        beginTag(tag);

        string targetDirectoryForTag = Path.Combine(targetFolder, tag.Name);
        if (!Directory.Exists(targetDirectoryForTag))
        {
            Directory.CreateDirectory(targetDirectoryForTag);
        }

        foreach (string itemInDb in itemsInDb)
        {
            PublishFileResult result = await PublishFile(cancellationToken, workingFolder, itemInDb, targetDirectoryForTag, linkMode);
            onFileCompleted(result);
        }
    }

    private static async Task<PublishFileResult> PublishFile(
        CancellationToken cancellationToken,
        string workingFolder,
        string itemInDb,
        string targetDirectoryForTag,
        PublishLinkMode linkMode
    )
    {
        string itemFullPath = Path.Combine(workingFolder, itemInDb);

        string fileName = Path.GetFileName(itemFullPath);

        if (!File.Exists(itemFullPath))
        {
            // skip, the source file is not found, this could be a warning!
            return new(itemFullPath, null, false, false);
        }

        string destFileName = Path.Combine(targetDirectoryForTag, fileName);
        if (File.Exists(destFileName))
        {
            FileInfo itemFullPathFi = new(itemFullPath);
            FileInfo destFileNameFi = new(destFileName);
            if (FileExt.FilesAreEqual(itemFullPathFi, destFileNameFi))
            {
                // skip, the source file is the same as the target
                return new(itemFullPath, destFileName, false, false);
            }

            string fileExtension = Path.GetExtension(fileName);
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            int num = 0;
            do
            {
                num++;
                destFileName = Path.Combine(targetDirectoryForTag, nameWithoutExtension + " (" + num + ")" + fileExtension);
                destFileNameFi = new(destFileName);
                if (destFileNameFi.Exists && FileExt.FilesAreEqual(itemFullPathFi, destFileNameFi))
                {
                    // skip, the source file is the same as the target, so already copied
                    return new(itemFullPath, destFileName, false, false);
                }
            } while (File.Exists(destFileName));
        }

        // A hard link costs no extra space, but only works on the same volume.
        // Anything else (another disk, a USB key, a network share) falls back to
        // the copy below, so the target stays usable.
        if (linkMode == PublishLinkMode.HardLinkWhenPossible && HardLink.TryCreate(itemFullPath, destFileName))
        {
            return new(itemFullPath, destFileName, true, true);
        }

        await CopyFileAsync(itemFullPath, destFileName, cancellationToken);
        return new(itemFullPath, destFileName, true, false);
    }

    private static async Task CopyFileAsync(string sourceFile, string destinationFile, CancellationToken cancellationToken)
    {
        int bufferSize = 4096;
        await using (FileStream sourceStream = new(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
        await using (
            FileStream destinationStream = new(
                destinationFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan
            )
        )
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);

        File.SetLastWriteTime(destinationFile, File.GetLastWriteTime(sourceFile));
    }

    static void DeleteDirectoryRecursively(
        string targetDir,
        Action<(string path, bool success, string? error)> onFileDeleted,
        Action<(string path, bool success, string? error)> onDirectoryDeleted,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // Delete all files in the directory
            foreach (string file in Directory.GetFiles(targetDir))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

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

            if (cancellationToken.IsCancellationRequested)
                return;

            // Delete all subdirectories in the directory
            foreach (string subDir in Directory.GetDirectories(targetDir))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                DeleteDirectoryRecursively(subDir, onFileDeleted, onDirectoryDeleted, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
                return;

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

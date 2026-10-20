using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models.CompareAlgorithms;
using ImgTagFanOut.ViewModels;
using Serilog;

namespace ImgTagFanOut.Models;

public class Publisher : IPublisher
{
    /// <summary>
    /// Characters a folder name cannot contain on Windows. Replaced on every
    /// platform so an export folder stays usable when it is copied elsewhere.
    /// </summary>
    private static readonly char[] InvalidFolderNameChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    /// <summary>Device names Windows refuses as file or folder names.</summary>
    private static readonly HashSet<string> ReservedFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

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
        if (string.IsNullOrWhiteSpace(workingFolder))
        {
            throw new ArgumentException("The library folder is required.", nameof(workingFolder));
        }

        if (string.IsNullOrWhiteSpace(targetFolder))
        {
            throw new ArgumentException("The export folder is required.", nameof(targetFolder));
        }

        string workingRoot = Path.GetFullPath(workingFolder);
        string targetRoot = Path.GetFullPath(targetFolder);

        // With "drop everything first", confusing the two folders would delete
        // the library itself. Refuse before touching anything.
        if (PathsEqual(workingRoot, targetRoot))
        {
            throw new InvalidOperationException(
                "The export folder must be different from the library folder.");
        }

        if (!Directory.Exists(targetRoot))
        {
            Directory.CreateDirectory(targetRoot);
        }
        else if (dropEverythingFirst)
        {
            DeleteDirectoryRecursively(targetRoot, onFileDeleted, onDirectoryDeleted, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // DeleteDirectoryRecursively removes the root too; put it back so an
            // empty library still leaves a usable export folder.
            Directory.CreateDirectory(targetRoot);
        }

        await using IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(workingFolder, cancellationToken);

        ImmutableList<Tag> tags = unitOfWork.TagRepository.GetAllTag();
        IReadOnlyDictionary<string, string> folderNames = GetSafeFolderNames(tags);

        foreach (Tag tag in tags)
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

            await PublishTag(
                workingRoot,
                targetRoot,
                linkMode,
                beginTag,
                onFileCompleted,
                tag,
                folderNames[tag.Name],
                itemsInDb,
                cancellationToken);
        }
    }

    private static async Task PublishTag(
        string workingRoot,
        string targetRoot,
        PublishLinkMode linkMode,
        Action<Tag> beginTag,
        Action<PublishFileResult> onFileCompleted,
        Tag tag,
        string folderName,
        ImmutableList<string> itemsInDb,
        CancellationToken cancellationToken
    )
    {
        beginTag(tag);

        string targetDirectoryForTag = Path.Combine(targetRoot, folderName);
        try
        {
            Directory.CreateDirectory(targetDirectoryForTag);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // One unusable tag folder must not stop the other tags.
            Log.Warning(ex, "Cannot create the export folder for tag {Tag}", tag.Name);
            foreach (string itemInDb in itemsInDb)
            {
                onFileCompleted(new(
                    Path.Combine(workingRoot, itemInDb),
                    null,
                    false,
                    false,
                    ex.Message));
            }

            return;
        }

        foreach (string itemInDb in itemsInDb)
        {
            PublishFileResult result = await PublishFile(cancellationToken, workingRoot, itemInDb, targetDirectoryForTag, linkMode);
            onFileCompleted(result);
        }
    }

    private static async Task<PublishFileResult> PublishFile(
        CancellationToken cancellationToken,
        string workingRoot,
        string itemInDb,
        string targetDirectoryForTag,
        PublishLinkMode linkMode
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        string itemFullPath = Path.Combine(workingRoot, itemInDb);

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(itemFullPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new(itemFullPath, null, false, false, ex.Message);
        }

        // A corrupted database or a hand-edited item must not make the export
        // read outside the library.
        if (!IsPathUnder(fullPath, workingRoot))
        {
            return new(itemFullPath, null, false, false, "The item is outside the library folder.");
        }

        if (!File.Exists(fullPath))
        {
            // skip, the source file is not found, this could be a warning!
            return new(itemFullPath, null, false, false);
        }

        try
        {
            string fileName = Path.GetFileName(fullPath);
            string destFileName = Path.Combine(targetDirectoryForTag, fileName);

            if (File.Exists(destFileName))
            {
                FileInfo itemFullPathFi = new(fullPath);
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

            cancellationToken.ThrowIfCancellationRequested();

            // A hard link costs no extra space, but only works on the same volume.
            // Anything else (another disk, a USB key, a network share) falls back to
            // the copy below, so the target stays usable.
            if (linkMode == PublishLinkMode.HardLinkWhenPossible && HardLink.TryCreate(fullPath, destFileName))
            {
                return new(itemFullPath, destFileName, true, true);
            }

            await CopyFileAsync(fullPath, destFileName, cancellationToken);
            return new(itemFullPath, destFileName, true, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // One unreadable or locked file must not abort the whole publication.
            Log.Warning(ex, "Cannot publish {File}", fullPath);
            return new(itemFullPath, null, false, false, ex.Message);
        }
    }

    private static async Task CopyFileAsync(string sourceFile, string destinationFile, CancellationToken cancellationToken)
    {
        int bufferSize = 81920;
        try
        {
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
        }
        catch
        {
            // Do not leave a truncated copy behind: the next run would compare
            // it, see a different length, and create "photo (1).jpg" forever.
            TryDelete(destinationFile);
            throw;
        }

        try
        {
            File.SetLastWriteTime(destinationFile, File.GetLastWriteTime(sourceFile));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            // The content is what matters; a timestamp is only cosmetic.
            Log.Warning(ex, "Cannot copy the timestamp of {File}", sourceFile);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Cannot remove the partial export {File}", path);
        }
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

                // Never follow a symbolic link: it may point outside the export
                // folder, and deleting through it would destroy unrelated files.
                if (new DirectoryInfo(subDir).Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    try
                    {
                        Directory.Delete(subDir, false);
                        onDirectoryDeleted((subDir, true, null));
                    }
                    catch (Exception ex)
                    {
                        onDirectoryDeleted((subDir, false, ex.Message));
                    }

                    continue;
                }

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

    /// <summary>
    /// Tag names become folder names: strip everything that would escape the
    /// export folder or be refused by the filesystem.
    /// </summary>
    internal static string GetSafeFolderName(string tagName)
    {
        string trimmed = tagName.Trim();
        StringBuilder builder = new(trimmed.Length);

        foreach (char c in trimmed)
        {
            builder.Append(c < ' ' || InvalidFolderNameChars.Contains(c) ? '_' : c);
        }

        // Windows silently drops trailing dots and spaces, which would make two
        // different tags collide after the fact.
        string name = builder.ToString().TrimEnd('.').TrimEnd();
        if (name.Length == 0 || name is "." or "..")
        {
            return "_";
        }

        // "CON" is a device on Windows even with an extension.
        string stem = name.Split('.')[0];
        return ReservedFolderNames.Contains(stem) ? "_" + name : name;
    }

    private static IReadOnlyDictionary<string, string> GetSafeFolderNames(IEnumerable<Tag> tags)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);

        // Sorted so a naming collision always resolves the same way.
        foreach (Tag tag in tags.OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase))
        {
            string baseName = GetSafeFolderName(tag.Name);
            string folderName = baseName;
            int suffix = 2;
            while (!used.Add(folderName))
            {
                folderName = $"{baseName} ({suffix})";
                suffix++;
            }

            result[tag.Name] = folderName;
        }

        return result;
    }

    private static bool PathsEqual(string first, string second)
    {
        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(
            Path.TrimEndingDirectorySeparator(first),
            Path.TrimEndingDirectorySeparator(second),
            comparison);
    }

    private static bool IsPathUnder(string path, string root)
    {
        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string normalizedPath = Path.GetFullPath(path);

        return normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, comparison);
    }
}

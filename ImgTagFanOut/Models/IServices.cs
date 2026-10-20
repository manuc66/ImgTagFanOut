using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Models;

public interface IFolderScan
{
    Task ScanFolder(CancellationToken cancellationToken, string workingFolder, DynamicData.SourceList<CanHaveTag> images);
}

public interface IHashEvaluator
{
    Task<string> ComputeHashAsync(string filePath, CancellationToken ctsToken);
}

public interface IThumbnailProvider
{
    Task<Bitmap?> GetThumbnail(string fullFilePath);

    Task<Bitmap?> GetThumbnail(string fullFilePath, uint targetWidth);
}

public interface IPublisher
{
    Task PublishToFolder(
        string workingFolder,
        string targetFolder,
        bool dropEverythingFirst,
        PublishLinkMode linkMode,
        Action<Tag> beginTag,
        Action<PublishFileResult> onFileCompleted,
        Action<(string path, bool success, string? error)> onFileDeleted,
        Action<(string path, bool success, string? error)> onDirectoryDeleted,
        CancellationToken cancellationToken
    );
}
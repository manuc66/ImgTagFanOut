using System;
using System.Threading;
using System.Threading.Tasks;
using ImgTagFanOut.Models;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Tests;

/// <summary>
/// Fake <see cref="IPublisher"/> whose <see cref="PublishToFolder"/> stays pending until
/// <see cref="Complete"/> (or <see cref="Fail"/>) is called. Lets a test observe the
/// window while a publish is still in flight, then let it finish on demand.
/// </summary>
public sealed class FakePublisher : IPublisher
{
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool WasCalled { get; private set; }
    public string? WorkingFolderUsed { get; private set; }
    public string? TargetFolderUsed { get; private set; }
    public bool DropEverythingFirstUsed { get; private set; }

    /// <summary>Invoked when the publish starts, so a test can feed progress callbacks.</summary>
    public Action<Action<Tag>, Action<(string source, string? destination, bool copied)>, Action<(string path, bool success, string? error)>, Action<(string path, bool success, string? error)>>? OnStart { get; set; }

    public Task PublishToFolder(
        string workingFolder,
        string targetFolder,
        bool dropEverythingFirst,
        Action<Tag> beginTag,
        Action<(string source, string? destination, bool copied)> onFileCompleted,
        Action<(string path, bool success, string? error)> onFileDeleted,
        Action<(string path, bool success, string? error)> onDirectoryDeleted,
        CancellationToken cancellationToken)
    {
        WasCalled = true;
        WorkingFolderUsed = workingFolder;
        TargetFolderUsed = targetFolder;
        DropEverythingFirstUsed = dropEverythingFirst;
        OnStart?.Invoke(beginTag, onFileCompleted, onFileDeleted, onDirectoryDeleted);
        return _gate.Task;
    }

    public void Complete() => _gate.TrySetResult();

    public void Fail(Exception exception) => _gate.TrySetException(exception);
}

/// <summary>Records the calls made by view models that shell out to the file manager.</summary>
public sealed class FakeFileManagerHandler : IFileManagerHandler
{
    public string? LastOpenedFolder { get; private set; }

    public Task OpenParentFolder(string path)
    {
        LastOpenedFolder = path;
        return Task.CompletedTask;
    }

    public Task OpenFolder(string parentFolder)
    {
        LastOpenedFolder = parentFolder;
        return Task.CompletedTask;
    }

    public Task OpenFile(string path) => Task.CompletedTask;

    public Task RevealFileInFolder(string path) => Task.CompletedTask;
}
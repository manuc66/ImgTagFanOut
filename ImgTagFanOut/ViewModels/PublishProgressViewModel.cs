using System;
using System.IO;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using ImgTagFanOut.Models;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

public class PublishProgressViewModel : ViewModelBase
{
    private readonly bool _dropEverythingFirst;
    private readonly IPublisher _publisher;
    private readonly IFileManagerHandler _fileManagerHandler;
    private readonly PublishLinkMode _linkMode;
    private string _trailLog;
    private bool _completed;
    public string WorkingFolder { get; }
    public string TargetFolder { get; }

    public bool Completed
    {
        get => _completed;
        set => this.RaiseAndSetIfChanged(ref _completed, value);
    }

    public ReactiveCommand<Window, Unit> CloseCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenTargetFolderCommand { get; }

    public string TrailLog
    {
        get => _trailLog;
        set => this.RaiseAndSetIfChanged(ref _trailLog, value);
    }

    public PublishProgressViewModel(string workingFolder, string targetFolder, bool dropEverythingFirst, PublishLinkMode linkMode, CancellationToken cancellationToken)
        : this(workingFolder, targetFolder, dropEverythingFirst, linkMode, cancellationToken, new Publisher(), new FileManagerHandler()) { }

    internal PublishProgressViewModel(
        string workingFolder,
        string targetFolder,
        bool dropEverythingFirst,
        PublishLinkMode linkMode,
        CancellationToken cancellationToken,
        IPublisher publisher,
        IFileManagerHandler fileManagerHandler)
    {
        _publisher = publisher;
        _fileManagerHandler = fileManagerHandler;
        _dropEverythingFirst = dropEverythingFirst;
        _linkMode = linkMode;
        WorkingFolder = workingFolder;
        TargetFolder = targetFolder;
        _trailLog = string.Empty;
        CloseCommand = ReactiveCommand.CreateFromTask((Window _) => Task.CompletedTask, this.WhenAnyValue(x => x.Completed));

        OpenTargetFolderCommand = ReactiveCommand.CreateFromTask(OpenTargetFolder);

        RxApp.MainThreadScheduler.ScheduleAsync(
            (_, ct) =>
            {
                CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
                return StartPublish(cancellationTokenSource.Token);
            }
        );
    }

    private async Task OpenTargetFolder()
    {
        if (string.IsNullOrEmpty(TargetFolder))
        {
            return;
        }

        if (Directory.Exists(TargetFolder))
        {
            await _fileManagerHandler.OpenFolder(TargetFolder);
        }
    }

    private async Task StartPublish(CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.PublishToFolder(
                WorkingFolder,
                TargetFolder,
                _dropEverythingFirst,
                _linkMode,
                OnBeginTag,
                OnFileCompleted,
                OnFileDeleted,
                OnDirectoryDeleted,
                cancellationToken
            );
            TrailLog += $"{Environment.NewLine}{Environment.NewLine}==> Done!";
        }
        catch (OperationCanceledException)
        {
            TrailLog += $"{Environment.NewLine}{Environment.NewLine} ==> {Resources.Resources.PublishCancelled}";
        }
        catch (Exception e)
        {
            // The dialog is the only place that can show the failure: let it
            // stay open with the error instead of bubbling an unhandled task.
            TrailLog += $"{Environment.NewLine}{Environment.NewLine} ==> {Resources.Resources.PublishFailed} -> {e.Message}";
        }
        finally
        {
            Completed = true;
        }
    }

    private void OnBeginTag(Tag tag)
    {
        string toAdd = string.IsNullOrEmpty(TrailLog) ? string.Empty : Environment.NewLine;
        string separator = new('=', 20);
        toAdd += $"{separator} {tag.Name} {separator}";
        TrailLog += toAdd;
    }

    private void OnFileDeleted((string path, bool success, string? error) a)
    {
        if (a.success)
        {
            TrailLog += $"{Environment.NewLine} File deleted: {a.path}";
        }
        else
        {
            TrailLog += $"{Environment.NewLine} File NOT DELETED:{a.path} ERROR: {a.error}";
        }
    }

    private void OnDirectoryDeleted((string path, bool success, string? error) a)
    {
        if (a.success)
        {
            TrailLog += $"{Environment.NewLine} Directory deleted: {a.path}";
        }
        else
        {
            TrailLog += $"{Environment.NewLine} Directory NOT DELETED:{a.path} ERROR: {a.error}";
        }
    }

    private void OnFileCompleted(PublishFileResult result)
    {
        string toAdd = string.IsNullOrEmpty(TrailLog) ? string.Empty : Environment.NewLine;

        if (result.Failed)
        {
            toAdd += $"{result.Source} {Resources.Resources.PublishFailed} {result.Error}";
        }
        else if (result.Linked)
        {
            toAdd += $"{result.Source} {Resources.Resources.LinkedTo} {result.Destination}";
        }
        else if (!result.Created)
        {
            if (result.Destination == null)
            {
                toAdd += $"{result.Source} {Resources.Resources.NotFoundSkipped}";
            }
            else
            {
                toAdd += $"{result.Source} {Resources.Resources.AlreadyAt} {result.Destination}";
            }
        }
        else
        {
            toAdd += $"{result.Source} {Resources.Resources.CopiedTo} {result.Destination}";
        }

        TrailLog += toAdd;
    }
}

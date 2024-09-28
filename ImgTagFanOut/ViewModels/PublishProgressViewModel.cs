using System;
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

    public string TrailLog
    {
        get => _trailLog;
        set => this.RaiseAndSetIfChanged(ref _trailLog, value);
    }

    public PublishProgressViewModel(string workingFolder, string targetFolder, CancellationToken cancellationToken)
    {
        WorkingFolder = workingFolder;
        TargetFolder = targetFolder;
        _trailLog = string.Empty;
        CloseCommand = ReactiveCommand.CreateFromTask((Window _) => Task.CompletedTask, this.WhenAnyValue(x => x.Completed));

        RxApp.MainThreadScheduler.ScheduleAsync((_, ct) =>
        {
            CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            return StartPublish(cancellationTokenSource.Token);
        });
    }

    private async Task StartPublish(CancellationToken cancellationToken)
    {
        try
        {
            await new Publisher().PublishToFolder(WorkingFolder, TargetFolder, OnBeginTag, OnFileCompleted,
                cancellationToken);
            TrailLog += $"{Environment.NewLine}{Environment.NewLine}==> Done!";
        }
        catch (Exception e)
        {
            TrailLog += $"{Environment.NewLine}{Environment.NewLine} ==> Error! -> {e.Message}";
            throw;
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

    private void OnFileCompleted((string source, string? destination, bool copied) a)
    {
        (string source, string? destination, bool copied) = a;
        string toAdd = string.IsNullOrEmpty(TrailLog) ? string.Empty : Environment.NewLine;

        if (!copied)
        {
            if (destination == null)
            {
                toAdd += $"{source} {Resources.Resources.NotFoundSkipped}";
            }
            else
            {
                toAdd += $"{source} {Resources.Resources.AlreadyAt} {destination}";
            }
        }
        else
        {
            toAdd += $"{source} {Resources.Resources.CopiedTo} {destination}";
        }

        TrailLog += toAdd;
    }
}

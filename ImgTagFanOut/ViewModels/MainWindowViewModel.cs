using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public string Greeting => "Welcome to Avalonia!";
    private string _workingFolder = string.Empty;

    public string WorkingFolder
    {
        get => _workingFolder;
        set => this.RaiseAndSetIfChanged(ref _workingFolder, value);
    }
    
    public ReactiveCommand<Window, string> SelectFolderCommand { get; }
    
    public ReactiveCommand<Unit, Unit> ScanFolderCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelScanCommand { get; }
    public MainWindowViewModel()
    {
        ScanFolderCommand = ReactiveCommand.CreateFromObservable(
            () => Observable
                .StartAsync(ScanFolder)
                .TakeUntil(CancelScanCommand!));
        ScanFolderCommand.Subscribe(x => { });
        SelectFolderCommand = ReactiveCommand.CreateFromTask<Window, string>(async (Window window) =>
        {
            FolderPickerOpenOptions folderPickerOptions = new()
            {
                AllowMultiple = false,
                Title = "Select an export folder",
                SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(WorkingFolder)
            };
            IReadOnlyList<IStorageFolder> folders = await window.StorageProvider.OpenFolderPickerAsync(folderPickerOptions);

            if (folders.Count == 0)
            {
                return WorkingFolder;
            }

            string? tryGetLocalPath = folders[0].TryGetLocalPath();
            if (tryGetLocalPath != null && Directory.Exists(tryGetLocalPath))
            {
                return tryGetLocalPath;
            }

            return WorkingFolder;
        }, ScanFolderCommand.IsExecuting.Select(x => !x));
        SelectFolderCommand.Subscribe(path => { WorkingFolder = path; });
        CancelScanCommand = ReactiveCommand.Create(
            () => { },
            CancelScanCommand?.IsExecuting);
    }

    private async Task ScanFolder(CancellationToken arg)
    {
        await Task.CompletedTask;
    }
}
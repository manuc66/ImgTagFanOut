using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string _workingFolder = string.Empty;
    private string _selectedImage = string.Empty;
    private Bitmap? _imageToDisplay = null;
    private ObservableCollection<string> _images = new();

    public string WorkingFolder
    {
        get => _workingFolder;
        set => this.RaiseAndSetIfChanged(ref _workingFolder, value);
    }

    public ReactiveCommand<Window, string> SelectFolderCommand { get; }

    public ReactiveCommand<Unit, Unit> ScanFolderCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelScanCommand { get; }

    public ObservableCollection<string> Images
    {
        get => _images;
        set => this.RaiseAndSetIfChanged(ref _images, value);
    }

    public string SelectedImage
    {
        get => _selectedImage;
        set => this.RaiseAndSetIfChanged(ref _selectedImage, value);
    }

    public Bitmap? ImageToDisplay
    {
        get => _imageToDisplay;
        set => this.RaiseAndSetIfChanged(ref _imageToDisplay, value);
    }

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

        this.WhenAnyValue(x => x.SelectedImage)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Throttle(TimeSpan.FromMilliseconds(10))
            .ObserveOn(RxApp.MainThreadScheduler).Subscribe(x =>
            {
                Bitmap? previous = ImageToDisplay;
                string fullFilePath = Path.Combine(WorkingFolder, x);
                
                using (FileStream fs = new(fullFilePath, FileMode.Open, FileAccess.Read))
                {
                    ImageToDisplay = new Bitmap(fs);
                }

                previous?.Dispose();
            });
    }

    private async Task ScanFolder(CancellationToken arg)
    {
        IEnumerable<string> enumerateFiles = Directory.EnumerateFiles(WorkingFolder, "*.jpg", SearchOption.AllDirectories);

        foreach (string file in enumerateFiles)
        {
            Images.Add(Path.GetRelativePath(WorkingFolder, file));
        }


        await Task.CompletedTask;
    }
}
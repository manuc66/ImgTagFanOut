using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Joins;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string _workingFolder = string.Empty;
    private string _selectedImage = string.Empty;
    private Bitmap? _imageToDisplay = null;
    private ObservableCollection<string> _images = new();
    private string? _tagFilterInput;
    private ObservableCollection<string> _tagList = new();
    private List<string> _filteredTagList;

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

    public ObservableCollection<string> TagList
    {
        get => _tagList;
        set => this.RaiseAndSetIfChanged(ref _tagList, value);
    }

    public List<string> FilteredTagList
    {
        get => _filteredTagList;
        set => this.RaiseAndSetIfChanged(ref _filteredTagList, value);
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

    public ReactiveCommand<Unit, Unit> AddToTagListCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearTagFilterInputCommand { get; }

    public String? TagFilterInput
    {
        get => _tagFilterInput;
        set => this.RaiseAndSetIfChanged(ref _tagFilterInput, value);
    }


    public MainWindowViewModel()
    {
        TagList = new ObservableCollection<string>() { "Luca", "Louis", "Filip" };

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

        AddToTagListCommand = ReactiveCommand.Create(() =>
        {
            if (!string.IsNullOrWhiteSpace(TagFilterInput))
            {
                TagList.Add(TagFilterInput.Trim());
            }
        }, this.WhenAnyValue(x => x.TagFilterInput).Select(x => !string.IsNullOrWhiteSpace(x) && !TagList.Any(tag => tag.Equals(x.Trim(), StringComparison.OrdinalIgnoreCase))));
        ClearTagFilterInputCommand = ReactiveCommand.Create(() => { TagFilterInput = String.Empty; },
            this.WhenAnyValue(x => x.TagFilterInput).Select(x => !string.IsNullOrWhiteSpace(x)));
        
        this.WhenAnyValue(x => x.TagFilterInput).Subscribe(tagFilterInput =>
        {
            if (string.IsNullOrWhiteSpace(tagFilterInput))
            {
                FilteredTagList = new List<string>(TagList);
            }
            else
            {
                FilteredTagList = new List<string>(TagList.Where(x => x.Contains(tagFilterInput, StringComparison.OrdinalIgnoreCase)));
            }
        });

        TagList
            // Convert the collection to a stream of chunks,
            // so we have IObservable<IChangeSet<TKey, TValue>>
            // type also known as the DynamicData monad.
            .ToObservableChangeSet(x => x)
            // Each time the collection changes, we get
            // all updated items at once.
            .ToCollection()
            // If the collection isn't empty, we access the
            // first element and check if it is an empty string.
            .Subscribe(tagList =>
            {
                if (string.IsNullOrWhiteSpace(TagFilterInput))
                {
                    FilteredTagList = new List<string>(tagList);
                }
                else
                {
                    FilteredTagList = new List<string>(tagList.Where(x => x.Contains(TagFilterInput, StringComparison.OrdinalIgnoreCase)));
                }
            });

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
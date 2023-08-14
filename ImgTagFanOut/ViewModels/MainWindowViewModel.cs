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
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string? _workingFolder;
    private CanHaveTag<string>? _selectedImage;
    private Bitmap? _imageToDisplay;
    private ReadOnlyObservableCollection<CanHaveTag<string>> _filteredImages;
    private string? _tagFilterInput;
    private readonly ObservableCollection<Tag> _tagList = new();
    private List<SelectableTag> _filteredTagList;
    private readonly TagRepository _tagRepository = new();
    private bool _hideDone;
    private readonly SourceList<CanHaveTag<string>> _images = new SourceList<CanHaveTag<string>>();
    private int _selectedIndex;

    public string? WorkingFolder
    {
        get => _workingFolder;
        set => this.RaiseAndSetIfChanged(ref _workingFolder, value);
    }

    public ReadOnlyObservableCollection<CanHaveTag<string>> FilteredImages
    {
        get => _filteredImages;
        set => _filteredImages = value;
    }

    private ObservableCollection<Tag> TagList
    {
        get => _tagList;
        init => this.RaiseAndSetIfChanged(ref _tagList, value);
    }

    public List<SelectableTag> FilteredTagList
    {
        get => _filteredTagList;
        set => this.RaiseAndSetIfChanged(ref _filteredTagList, value);
    }

    public CanHaveTag<string>? SelectedImage
    {
        get => _selectedImage;
        set => this.RaiseAndSetIfChanged(ref _selectedImage, value);
    }

    public Bitmap? ImageToDisplay
    {
        get => _imageToDisplay;
        set => this.RaiseAndSetIfChanged(ref _imageToDisplay, value);
    }

    public String? TagFilterInput
    {
        get => _tagFilterInput;
        set => this.RaiseAndSetIfChanged(ref _tagFilterInput, value);
    }

    public bool HideDone
    {
        get => _hideDone;
        set => this.RaiseAndSetIfChanged(ref _hideDone, value);
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedIndex, value);
    }

    public ReactiveCommand<Window, string> SelectFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelScanCommand { get; }
    public ReactiveCommand<Tag, Unit> ToggleAssignTagToImageCommand { get; }
    public ReactiveCommand<Tag, Unit> RemoveTagToImageCommand { get; }
    public ReactiveCommand<Unit, Unit> AddToTagListCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearTagFilterInputCommand { get; }
    public ReactiveCommand<Unit, Unit> DoneCommand { get; }

    public MainWindowViewModel()
    {
        _tagRepository.TryCreateTag("Lucas", out _);
        _tagRepository.TryCreateTag("Louis", out _);
        _tagRepository.TryCreateTag("Filip", out _);
        TagList = new ObservableCollection<Tag>(_tagRepository.GetAll());
        _filteredTagList = new List<SelectableTag>();
        HideDone = true;

        _images.Connect()
            .AutoRefresh(x => x.Done)
            .Filter(this.WhenValueChanged(@this => @this.HideDone)
                .Select(CreatePredicate))
            .Sort(SortExpressionComparer<CanHaveTag<string>>.Ascending(t => t.Item))
            .Bind(out _filteredImages)
            .Subscribe();

        DoneCommand = ReactiveCommand.Create(() =>
        {
            if (SelectedImage != null)
            {
                int selectedIndex = SelectedIndex;
                SelectedImage.Done = true;
                SelectedIndex = Math.Min(selectedIndex, FilteredImages.Count - 1);
            }
        }, this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));

        ScanFolderCommand = ReactiveCommand.CreateFromObservable(
            () => Observable
                .StartAsync(ScanFolder, RxApp.TaskpoolScheduler)
                .TakeUntil(CancelScanCommand!), this.WhenAnyValue(x => x.WorkingFolder).Select(x => !string.IsNullOrWhiteSpace(x) && Directory.Exists(x)));
        SelectFolderCommand = ReactiveCommand.CreateFromTask<Window, string>(SelectFolder, ScanFolderCommand.IsExecuting.Select(x => !x));
        SelectFolderCommand.Subscribe(path => { WorkingFolder = path; });
        CancelScanCommand = ReactiveCommand.Create(
            () => { },
            ScanFolderCommand.IsExecuting);

        AddToTagListCommand = ReactiveCommand.Create(() =>
        {
            if (_tagRepository.TryCreateTag(TagFilterInput, out Tag? newTag))
            {
                TagList.Add(newTag);
            }
        }, this.WhenAnyValue(x => x.TagFilterInput)
            .CombineLatest(TagList.ToObservableChangeSet(x => x).ToCollection(),
                (tagFilterInput, tagList) => !string.IsNullOrWhiteSpace(tagFilterInput) && !tagList.Any(tag => tag.Same(tagFilterInput))));
        ClearTagFilterInputCommand = ReactiveCommand.Create(() => { TagFilterInput = String.Empty; },
            this.WhenAnyValue(x => x.TagFilterInput).Select(x => !string.IsNullOrWhiteSpace(x)));

        ToggleAssignTagToImageCommand = ReactiveCommand.Create((Tag s) =>
        {
            if (SelectedImage != null)
            {
                _tagRepository.ToggleToItem(s, SelectedImage);
            }

            foreach (SelectableTag selectableTag in FilteredTagList)
            {
                selectableTag.IsSelected = IsSelected(selectableTag.Tag, SelectedImage);
            }
        }, this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));

        RemoveTagToImageCommand = ReactiveCommand.Create((Tag s) =>
        {
            _tagRepository.RemoveTagToItem(s.Name, SelectedImage);

            foreach (SelectableTag selectableTag in FilteredTagList)
            {
                selectableTag.IsSelected = IsSelected(selectableTag.Tag, SelectedImage);
            }
        });

        this.WhenAnyValue(x => x.TagFilterInput, x => x.SelectedImage)
            .CombineLatest(TagList
                .ToObservableChangeSet(x => x)
                .ToCollection()
            )
            .Subscribe((watched) =>
            {
                (string? tagFilterInput, CanHaveTag<string>? selectedImage) = watched.First;
                IReadOnlyCollection<Tag> list = watched.Second;

                FilteredTagList = list
                    .Where(tag => string.IsNullOrWhiteSpace(tagFilterInput) || tag.MatchFilter(tagFilterInput))
                    .Select(tag => new SelectableTag(tag) { IsSelected = IsSelected(tag, selectedImage) })
                    .ToList();
            });

        this.WhenAnyValue(x => x.SelectedImage)
            .Where(x => !string.IsNullOrWhiteSpace(x?.Item))
            .Throttle(TimeSpan.FromMilliseconds(10))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x =>
            {
                if (WorkingFolder == null || x == null)
                {
                    return;
                }

                Bitmap? previous = ImageToDisplay;
                string fullFilePath = Path.Combine(WorkingFolder, x.Item);

                using (FileStream fs = new(fullFilePath, FileMode.Open, FileAccess.Read))
                {
                    ImageToDisplay = new Bitmap(fs);
                }

                previous?.Dispose();
            });
    }

    private async Task<string> SelectFolder(Window window)
    {
        FolderPickerOpenOptions folderPickerOptions = new() { AllowMultiple = false, Title = "Select an export folder", SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(WorkingFolder ?? String.Empty) };
        IReadOnlyList<IStorageFolder> folders = await window.StorageProvider.OpenFolderPickerAsync(folderPickerOptions);

        if (folders.Count == 0)
        {
            return WorkingFolder ?? string.Empty;
        }

        string? tryGetLocalPath = folders[0].TryGetLocalPath();
        if (tryGetLocalPath != null && Directory.Exists(tryGetLocalPath))
        {
            return tryGetLocalPath;
        }

        return WorkingFolder ?? String.Empty;
    }

    private Func<CanHaveTag<string>, bool> CreatePredicate(bool arg)
    {
        if (!arg)
        {
            return item => true;
        }

        return item => !item.Done;
    }

    private bool IsSelected(Tag x, CanHaveTag<string>? canHaveTag)
    {
        return canHaveTag?.Has(x) ?? false;
    }

    private async Task ScanFolder(CancellationToken arg)
    {
        _images.Clear();

        if (WorkingFolder == null)
        {
            return;
        }

        IEnumerable<string> enumerateFiles = Directory.EnumerateFiles(WorkingFolder, "*.jpg", SearchOption.AllDirectories);

        foreach (string file in enumerateFiles)
        {
            _images.Add(new CanHaveTag<string>(Path.GetRelativePath(WorkingFolder, file)));
        }

        await Task.CompletedTask;
    }
}
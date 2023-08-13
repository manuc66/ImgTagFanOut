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
    private Bitmap? _imageToDisplay = null;
    private ObservableCollection<CanHaveTag<string>> _images = new();
    private string? _tagFilterInput;
    private ObservableCollection<Tag> _tagList = new();
    private List<SelectableTag> _filteredTagList;
    private readonly TagRepository _tagRepository = new();
    private CanHaveTag<string>? _selectedImageTag;

    public string? WorkingFolder
    {
        get => _workingFolder;
        set => this.RaiseAndSetIfChanged(ref _workingFolder, value);
    }

    public ReactiveCommand<Window, string> SelectFolderCommand { get; }

    public ReactiveCommand<Unit, Unit> ScanFolderCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelScanCommand { get; }

    public ReactiveCommand<Tag, Unit> ToggleAssignTagToImageCommand { get; }

    public ReactiveCommand<Tag, Unit> RemoveTagToImageCommand { get; }

    public ObservableCollection<CanHaveTag<string>> Images
    {
        get => _images;
        set => this.RaiseAndSetIfChanged(ref _images, value);
    }

    public ObservableCollection<Tag> TagList
    {
        get => _tagList;
        set => this.RaiseAndSetIfChanged(ref _tagList, value);
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

    public ReactiveCommand<Unit, Unit> AddToTagListCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearTagFilterInputCommand { get; }
    public ReactiveCommand<Unit, Unit>  DoneCommand { get; }
    public ReactiveCommand<Unit, Unit>  PreviousCommand { get; }
    public ReactiveCommand<Unit, Unit>  NextCommand { get; }

    public String? TagFilterInput
    {
        get => _tagFilterInput;
        set => this.RaiseAndSetIfChanged(ref _tagFilterInput, value);
    }

    public MainWindowViewModel()
    {
        _tagRepository.TryCreateTag("Lucas", out _);
        _tagRepository.TryCreateTag("Louis", out _);
        _tagRepository.TryCreateTag("Filip", out _);
        TagList = new ObservableCollection<Tag>(_tagRepository.GetAll());
        _filteredTagList = new List<SelectableTag>();

        DoneCommand = ReactiveCommand.Create(() => { }, this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));
        PreviousCommand = ReactiveCommand.Create(() => { }, this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));
        NextCommand = ReactiveCommand.Create(() => { }, this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));

        ScanFolderCommand = ReactiveCommand.CreateFromObservable(
            () => Observable
                .StartAsync(ScanFolder, RxApp.TaskpoolScheduler)
                .TakeUntil(CancelScanCommand!), this.WhenAnyValue(x => x.WorkingFolder).Select(x=> !string.IsNullOrWhiteSpace(x) && Directory.Exists(x)) );
        ScanFolderCommand.Subscribe(x => { });
        SelectFolderCommand = ReactiveCommand.CreateFromTask<Window, string>(async window =>
        {
            FolderPickerOpenOptions folderPickerOptions = new()
            {
                AllowMultiple = false,
                Title = "Select an export folder",
                SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(WorkingFolder ?? String.Empty)
            };
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
        }, ScanFolderCommand.IsExecuting.Select(x => !x));
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

        ToggleAssignTagToImageCommand = ReactiveCommand.Create(
            (Tag s) =>
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

        RemoveTagToImageCommand = ReactiveCommand.Create(
            (Tag s) =>
            {
                _tagRepository.RemoveTagToItem(s.Name, SelectedImage);

                foreach (SelectableTag selectableTag in FilteredTagList)
                {
                    selectableTag.IsSelected = IsSelected(selectableTag.Tag, SelectedImage);
                }
            });

        this.WhenAnyValue(x => x.TagFilterInput, x => x.SelectedImage).CombineLatest(
                TagList
                    .ToObservableChangeSet(x => x)
                    .ToCollection(),
                (filter, list) => (filter, list))
            .Subscribe((current) =>
            {
                if(string.IsNullOrWhiteSpace(current.filter.Item1))
                {
                    FilteredTagList = current.list.Select(x => new SelectableTag(x) { IsSelected = IsSelected(x, current.filter.Item2) }).ToList();
                }
                else
                {
                    FilteredTagList = current.list.Where(x => x.MatchFilter(current.filter.Item1)).Select(x => new SelectableTag(x) { IsSelected = IsSelected(x, current.filter.Item2) }).ToList();
                }
            });
        


        this.WhenAnyValue(x => x.SelectedImage)
            .Where(x => !string.IsNullOrWhiteSpace(x?.Item))
            .Throttle(TimeSpan.FromMilliseconds(10))
            .ObserveOn(RxApp.MainThreadScheduler).Subscribe(x =>
            {
                if (WorkingFolder == null || x == null)
                {
                    return;
                }
                Bitmap? previous = ImageToDisplay;
                string fullFilePath = Path.Combine(WorkingFolder, x.Item!);

                using (FileStream fs = new(fullFilePath, FileMode.Open, FileAccess.Read))
                {
                    ImageToDisplay = new Bitmap(fs);
                }

                previous?.Dispose();
            });
    }

    private bool IsSelected(Tag x, CanHaveTag<string>? canHaveTag)
    {
        return canHaveTag?.Has(x) ?? false;
    }

    private async Task ScanFolder(CancellationToken arg)
    {
        if (WorkingFolder == null)
        {
            return;
        }
        
        IEnumerable<string> enumerateFiles = Directory.EnumerateFiles(WorkingFolder, "*.jpg", SearchOption.AllDirectories);

        foreach (string file in enumerateFiles)
        {
            Images.Add(new CanHaveTag<string>(Path.GetRelativePath(WorkingFolder, file)));
        }

        await Task.CompletedTask;
    }
}
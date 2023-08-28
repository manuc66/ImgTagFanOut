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
using ImgTagFanOut.Dao;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string? _workingFolder;
    private CanHaveTag? _selectedImage;
    private Bitmap? _imageToDisplay;
    private ReadOnlyObservableCollection<CanHaveTag> _filteredImages;
    private string? _tagFilterInput;
    private readonly ObservableCollection<Tag> _tagList = new();
    private List<SelectableTag> _filteredTagList;

    private bool _hideDone;
    private readonly SourceList<CanHaveTag> _images = new();
    private int _selectedIndex;
    private readonly TagCache _tagCache;

    public string? WorkingFolder
    {
        get => _workingFolder;
        set => this.RaiseAndSetIfChanged(ref _workingFolder, value);
    }

    public ReadOnlyObservableCollection<CanHaveTag> FilteredImages
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

    public CanHaveTag? SelectedImage
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
    public ReactiveCommand<SelectableTag, Unit>  DeleteTagCommand { get; }

    internal ImgTagFanOutDbContext GetRepo( out ITagRepository repo, string? path = null)
    {
        ImgTagFanOutDbContext imgTagFanOutDbContext = new (path);
        imgTagFanOutDbContext.Database.EnsureCreated();
        repo = new TagRepository(imgTagFanOutDbContext, _tagCache);
        return imgTagFanOutDbContext;
    }

    public MainWindowViewModel()
    {
        _tagCache = new TagCache();
        TagList = new ObservableCollection<Tag>();
       using (ImgTagFanOutDbContext imgTagFanOutDbContext = GetRepo(out ITagRepository tagRepository))
       {
           imgTagFanOutDbContext.Database.EnsureCreated();
           imgTagFanOutDbContext.SaveChanges();
           ReloadTagList(tagRepository);
       }

       _filteredTagList = new List<SelectableTag>();
        HideDone = true;
        TagFilterInput = String.Empty;
        

        _images.Connect()
            .AutoRefresh(x => x.Done)
            .Filter(this.WhenValueChanged(@this => @this.HideDone)
                .Select(CreateFilterForDone))
            .Sort(SortExpressionComparer<CanHaveTag>.Ascending(t => t.Item))
            .Bind(out _filteredImages)
            .Subscribe();

        DoneCommand = ReactiveCommand.Create(() =>
        {
            if (SelectedImage == null)
            {
                return;
            }

            int selectedIndex = SelectedIndex;
            using (ImgTagFanOutDbContext imgTagFanOutDbContext = GetRepo(out ITagRepository tagRepository, WorkingFolder))
            {
                tagRepository.MarkDone(SelectedImage);
                imgTagFanOutDbContext.SaveChanges();
            }

            SelectedIndex = Math.Min(selectedIndex, FilteredImages.Count - 1);
        }, this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));

        ScanFolderCommand = ReactiveCommand.CreateFromObservable(
            () => Observable
                .StartAsync(ScanFolder, RxApp.TaskpoolScheduler)
                .TakeUntil(CancelScanCommand!), this.WhenAnyValue(x => x.WorkingFolder).Select(x => !string.IsNullOrWhiteSpace(x) && Directory.Exists(x)));
        SelectFolderCommand = ReactiveCommand.CreateFromTask<Window, string>(SelectFolder, ScanFolderCommand.IsExecuting.Select(x => !x));
        SelectFolderCommand.Subscribe(path =>
        {
            WorkingFolder = path;
            using (ImgTagFanOutDbContext imgTagFanOutDbContext = GetRepo(out ITagRepository tagRepository, path))
            {
                imgTagFanOutDbContext.Database.EnsureCreated();
                ReloadTagList(tagRepository);
            }
        });
        CancelScanCommand = ReactiveCommand.Create(
            () => { },
            ScanFolderCommand.IsExecuting);

        AddToTagListCommand = ReactiveCommand.Create(() =>
        {
            using (ImgTagFanOutDbContext imgTagFanOutDbContext = GetRepo(out ITagRepository tagRepository, WorkingFolder))
            {
                if (tagRepository.TryCreateTag(TagFilterInput, out Tag? newTag))
                {
                    TagList.Add(newTag);
                }

                imgTagFanOutDbContext.SaveChanges();
            }
        }, this.WhenAnyValue(x => x.TagFilterInput)
            .CombineLatest(TagList
                    .ToObservableChangeSet(x => x)
                    .ToCollection()
                    .Prepend(new ReadOnlyCollection<Tag>(new List<Tag>())),
                ( tagFilterInput, tagList) => !string.IsNullOrWhiteSpace(tagFilterInput) && !tagList.Any(tag => tag.Same(tagFilterInput))));
        ClearTagFilterInputCommand = ReactiveCommand.Create(() => { TagFilterInput = String.Empty; },
            this.WhenAnyValue(x => x.TagFilterInput).Select(x => !string.IsNullOrWhiteSpace(x)));

        ToggleAssignTagToImageCommand = ReactiveCommand.Create((Tag s) =>
        {
            using (ImgTagFanOutDbContext imgTagFanOutDbContext = GetRepo(out ITagRepository tagRepository, WorkingFolder))
            {
                if (SelectedImage != null)
                {
                    tagRepository.ToggleToItem(s, SelectedImage);
                }

                imgTagFanOutDbContext.SaveChanges();

                foreach (SelectableTag selectableTag in FilteredTagList)
                {
                    selectableTag.IsSelected = IsSelected(selectableTag.Tag, SelectedImage);
                }

                imgTagFanOutDbContext.SaveChanges();
            }
        }, this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));

        RemoveTagToImageCommand = ReactiveCommand.Create((Tag s) =>
        {
            if (SelectedImage == null)
            {
                return;
            }
            using (ImgTagFanOutDbContext imgTagFanOutDbContext = GetRepo(out ITagRepository tagRepository, WorkingFolder))
            {
                tagRepository.RemoveTagToItem(s.Name, SelectedImage);

                imgTagFanOutDbContext.SaveChanges();

                foreach (SelectableTag selectableTag in FilteredTagList)
                {
                    selectableTag.IsSelected = IsSelected(selectableTag.Tag, SelectedImage);
                }
            }
        });

        DeleteTagCommand = ReactiveCommand.Create((SelectableTag s) =>
        {
            using (ImgTagFanOutDbContext imgTagFanOutDbContext = GetRepo(out ITagRepository tagRepository, WorkingFolder))
            {
                tagRepository.DeleteTag(s.Tag);
                
                TagList.Remove(s.Tag);

                imgTagFanOutDbContext.SaveChanges();

                foreach (CanHaveTag item in _images.Items)
                {
                    item.RemoveTag(s.Tag);
                }
            }
        }, ScanFolderCommand.IsExecuting.Select(x => !x));

        this.WhenAnyValue(x => x.TagFilterInput, x => x.SelectedImage)
            .CombineLatest(TagList
                .ToObservableChangeSet(x => x)
                .ToCollection()
            )
            .Subscribe((watched) =>
            {
                (string? tagFilterInput, CanHaveTag? selectedImage) = watched.First;
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

    private void ReloadTagList(ITagRepository tagRepository)
    {
        _tagCache.Clear();
        TagList.Clear();
        TagList.AddRange(tagRepository.GetAll());
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

    private Func<CanHaveTag, bool> CreateFilterForDone(bool arg) => arg ? item => !item.Done : _ => true;

    private bool IsSelected(Tag x, CanHaveTag? canHaveTag)
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

        using (ImgTagFanOutDbContext imgTagFanOutDbContext = GetRepo(out ITagRepository? tagRepository, WorkingFolder))
        {
            IEnumerable<string> enumerateFiles = Directory.EnumerateFiles(WorkingFolder, "*.jpg", SearchOption.AllDirectories)
                .Union(Directory.EnumerateFiles(WorkingFolder, "*.png", SearchOption.AllDirectories));

            foreach (string file in enumerateFiles)
            {
                CanHaveTag canHaveTag = new (Path.GetRelativePath(WorkingFolder, file));

                _images.Add(canHaveTag);

                tagRepository.AddOrUpdateItem(canHaveTag);
            }

            imgTagFanOutDbContext.SaveChanges();
        }

        await Task.CompletedTask;
    }
}
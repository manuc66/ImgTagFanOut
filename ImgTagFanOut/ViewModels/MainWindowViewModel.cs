using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using DynamicData;
using DynamicData.Binding;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private const string TargetFolderSettingKey = "target_folder";
    private const string ShowDoneSettingKey = "show_done";
    private string? _workingFolder;
    private string? _targetFolder;
    private CanHaveTag? _selectedImage;
    private Bitmap? _imageToDisplay;
    private Bitmap? _noPreviewToDisplay;
    private ReadOnlyObservableCollection<CanHaveTag> _filteredImages;
    private string? _tagFilterInput;
    private string? _itemFilterInput;
    private readonly ObservableCollection<Tag> _tagList = new();
    private List<SelectableTag> _filteredTagList;

    private bool _hideDone;
    private bool _showDone;
    private readonly SourceList<CanHaveTag> _images = new();
    private int _selectedIndex;
    private readonly Settings _settings;

    public string? WorkingFolder
    {
        get => _workingFolder;
        set => this.RaiseAndSetIfChanged(ref _workingFolder, value);
    }

    public string? TargetFolder
    {
        get => _targetFolder;
        set => this.RaiseAndSetIfChanged(ref _targetFolder, value);
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

    public Bitmap? NoPreviewToDisplay
    {
        get => _noPreviewToDisplay;
        set => this.RaiseAndSetIfChanged(ref _noPreviewToDisplay, value);
    }

    public string? TagFilterInput
    {
        get => _tagFilterInput;
        set => this.RaiseAndSetIfChanged(ref _tagFilterInput, value);
    }

    public string? ItemFilterInput
    {
        get => _itemFilterInput;
        set => this.RaiseAndSetIfChanged(ref _itemFilterInput, value);
    }

    public bool HideDone
    {
        get => _hideDone;
        set => this.RaiseAndSetIfChanged(ref _hideDone, value);
    }

    public bool ShowDone
    {
        get => _showDone;
        set { this.RaiseAndSetIfChanged(ref _showDone, value); }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedIndex, value);
    }

    public ReactiveCommand<Window, string> SelectFolderCommand { get; }
    public ReactiveCommand<Window, string?> SelectTargetFolderCommand { get; }

    public ReactiveCommand<Unit, Unit> ScanFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> PublishCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelScanCommand { get; }
    public ReactiveCommand<Tag, Unit> ToggleAssignTagToImageCommand { get; }
    public ReactiveCommand<Tag, Unit> RemoveTagToImageCommand { get; }
    public ReactiveCommand<Unit, Unit> AddToTagListCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearTagFilterInputCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearItemFilterInputCommand { get; }
    public ReactiveCommand<Unit, Unit> DoneCommand { get; }
    public ReactiveCommand<Unit, Unit> AllCommand { get; }
    public ReactiveCommand<Unit, Unit> NoneCommand { get; }
    public ReactiveCommand<SelectableTag, Unit> DeleteTagCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenCommand { get; }
    public ReactiveCommand<Unit, Unit> LocateCommand { get; }

    public MainWindowViewModel()
    {
        _settings = new();
        WorkingFolder = "";
        TagList = new ObservableCollection<Tag>();

        this.WhenAnyValue(x => x.ShowDone)
            .SelectMany(async x =>
            {
                await using (IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(WorkingFolder))
                {
                    unitOfWork.ParameterRepository.Update(ShowDoneSettingKey, x.ToString());
                    unitOfWork.SaveChanges();
                }

                return x;
            })
            .Subscribe(x => { HideDone = !x; });

        NoPreviewToDisplay = LoadNoPreviewToDisplay();
        this.WhenAnyValue(x => x.ImageToDisplay, x => x.SelectedImage).Subscribe(x =>
        {
            if (x is { Item1: not null, Item2: not null }) return;
            Bitmap? previous = ImageToDisplay;

            ImageToDisplay = NoPreviewToDisplay;

            if (previous != NoPreviewToDisplay)
            {
                previous?.Dispose();
            }
        });

        ImageToDisplay = NoPreviewToDisplay;

        _filteredTagList = new List<SelectableTag>();
        HideDone = true;
        TagFilterInput = string.Empty;


        _images.Connect()
            .AutoRefresh(x => x.Done)
            .Filter(this.WhenAnyValue(@this => @this.HideDone)
                .Select(CreateFilterForDone))
            .Filter(this.WhenValueChanged(@this => @this.ItemFilterInput)
                .Select(CreateFilterForItemFilterInput))
            .Sort(SortExpressionComparer<CanHaveTag>.Ascending(t => t.Item))
            .Bind(out _filteredImages)
            .Subscribe();

        DoneCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (SelectedImage == null)
            {
                return;
            }

            int selectedIndex = SelectedIndex;
            await using (IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(WorkingFolder))
            {
                unitOfWork.TagRepository.MarkDone(SelectedImage);
                unitOfWork.SaveChanges();
            }

            SelectedIndex = Math.Min(selectedIndex, FilteredImages.Count - 1);
        }, this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));
        OpenCommand = ReactiveCommand.CreateFromTask(OpenFile, this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));
        LocateCommand = ReactiveCommand.CreateFromTask(LocateFile, this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));

        ScanFolderCommand = ReactiveCommand.CreateFromObservable(
            () => Observable
                .StartAsync(ScanFolder, RxApp.TaskpoolScheduler)
                .TakeUntil(CancelScanCommand!), this.WhenAnyValue(x => x.WorkingFolder).Select(x => !string.IsNullOrWhiteSpace(x) && Directory.Exists(x)));

        SelectFolderCommand = ReactiveCommand.CreateFromTask<Window, string>(SelectFolder, ScanFolderCommand.IsExecuting.Select(x => !x));
        SelectFolderCommand.SelectMany(OpenFolder)
            .Subscribe(x => WorkingFolder = x);
        CancelScanCommand = ReactiveCommand.Create(
            () => { },
            ScanFolderCommand.IsExecuting);

        SelectTargetFolderCommand = ReactiveCommand.CreateFromTask<Window, string?>(SelectTargetFolder, ScanFolderCommand.IsExecuting.Select(x => !x));

        PublishCommand = ReactiveCommand.CreateFromObservable(
            () => Observable
                .StartAsync(PublishToFolder, RxApp.TaskpoolScheduler)
                .TakeUntil(CancelScanCommand), this.WhenAnyValue(x => x.WorkingFolder).Select(x => !string.IsNullOrWhiteSpace(x) && Directory.Exists(x)));


        AllCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SelectedImage == null)
                {
                    return;
                }

                await AssignAllTags(SelectedImage, TagList);
            },
            this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));
        NoneCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SelectedImage == null)
                {
                    return;
                }

                await using (IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(WorkingFolder))
                {
                    foreach (Tag selectedImageTag in SelectedImage.Tags.ToImmutableList())
                    {
                        unitOfWork.TagRepository.RemoveTagToItem(selectedImageTag, SelectedImage);
                    }

                    unitOfWork.SaveChanges();
                }

                RefreshTagIsSelected();
            },
            this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));

        AddToTagListCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await using IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(WorkingFolder);
            if (unitOfWork.TagRepository.TryCreateTag(TagFilterInput, out Tag? newTag))
            {
                TagList.Add(newTag);
            }

            unitOfWork.SaveChanges();
        }, this.WhenAnyValue(x => x.TagFilterInput)
            .CombineLatest(TagList
                    .ToObservableChangeSet(x => x)
                    .ToCollection()
                    .Prepend(new ReadOnlyCollection<Tag>(new List<Tag>())),
                (tagFilterInput, tagList) => !string.IsNullOrWhiteSpace(tagFilterInput) && !tagList.Any(tag => tag.Same(tagFilterInput))));
        ClearTagFilterInputCommand = ReactiveCommand.Create(() => { TagFilterInput = string.Empty; },
            this.WhenAnyValue(x => x.TagFilterInput).Select(x => !string.IsNullOrWhiteSpace(x)));

        ClearItemFilterInputCommand = ReactiveCommand.Create(() => { ItemFilterInput = string.Empty; },
            this.WhenAnyValue(x => x.ItemFilterInput).Select(x => !string.IsNullOrWhiteSpace(x)));

        ToggleAssignTagToImageCommand = ReactiveCommand.CreateFromTask(async (Tag s) =>
        {
            await using (IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(WorkingFolder))
            {
                if (SelectedImage != null)
                {
                    unitOfWork.TagRepository.ToggleToItem(s, SelectedImage);
                }

                unitOfWork.SaveChanges();
            }

            RefreshTagIsSelected();
        }, this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));

        RemoveTagToImageCommand = ReactiveCommand.CreateFromTask(async (Tag tag) =>
        {
            if (SelectedImage == null)
            {
                return;
            }

            await using (IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(WorkingFolder))
            {
                unitOfWork.TagRepository.RemoveTagToItem(tag, SelectedImage);

                unitOfWork.SaveChanges();
            }

            RefreshTagIsSelected();
        });

        DeleteTagCommand = ReactiveCommand.CreateFromTask(async (SelectableTag s) =>
        {
            await using (IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(WorkingFolder))
            {
                unitOfWork.TagRepository.DeleteTag(s.Tag);

                TagList.Remove(s.Tag);

                unitOfWork.SaveChanges();
            }

            foreach (CanHaveTag item in _images.Items)
            {
                item.RemoveTag(s.Tag);
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

                if (File.Exists(fullFilePath))
                {
                    try
                    {
                        using FileStream fs = new(fullFilePath, FileMode.Open, FileAccess.Read);
                        ImageToDisplay = new Bitmap(fs);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        ImageToDisplay = NoPreviewToDisplay;
                    }
                }
                else
                {
                    ImageToDisplay = NoPreviewToDisplay;
                }

                if (previous != NoPreviewToDisplay)
                {
                    previous?.Dispose();
                }
            });
    }

    private async Task<string> OpenFolder(string path)
    {
        await using IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(path);

        TargetFolder = unitOfWork.ParameterRepository.Get(TargetFolderSettingKey);
        ShowDone = Boolean.Parse(unitOfWork.ParameterRepository.Get(ShowDoneSettingKey) ?? false.ToString());

        ReloadTagList(unitOfWork.TagRepository);

        return path;
    }

    private void RefreshTagIsSelected()
    {
        foreach (SelectableTag selectableTag in FilteredTagList)
        {
            selectableTag.IsSelected = IsSelected(selectableTag.Tag, SelectedImage);
        }
    }

    private async Task AssignAllTags(CanHaveTag selectedImage, IEnumerable<Tag> allTags)
    {
        if (WorkingFolder == null)
        {
            return;
        }
        
        await using (IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(WorkingFolder))
        {
            foreach (Tag selectedImageTag in allTags)
            {
                unitOfWork.TagRepository.AddTagToItem(selectedImageTag, selectedImage);
            }

            await unitOfWork.SaveChangesAsync();
        }

        RefreshTagIsSelected();
    }

    private async Task OpenFile()
    {
        if (SelectedImage == null || WorkingFolder == null)
        {
            return;
        }

        string path = Path.Combine(WorkingFolder, SelectedImage.Item);

        if (File.Exists(path))
        {
            await new FileManagerHandler().OpenFile(path);
        }
    }


    private async Task LocateFile()
    {
        if (SelectedImage == null || WorkingFolder == null)
        {
            return;
        }

        string path = Path.Combine(WorkingFolder, SelectedImage.Item);

        if (File.Exists(path))
        {
            await new FileManagerHandler().RevealFileInFolder(path);
        }
    }

    private static Bitmap LoadNoPreviewToDisplay()
    {
        string resourceName = "ImgTagFanOut.NoPreview.png";
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName) ?? throw new Exception("Resource not found: " + resourceName);
        Bitmap noPreviewToDisplay = new(stream);
        return noPreviewToDisplay;
    }

    private void ReloadTagList(ITagRepository tagRepository)
    {
        DbContextFactory.ClearTagCache();
        TagList.Clear();
        TagList.AddRange(tagRepository.GetAllTag());
    }

    private async Task<string> SelectFolder(Window window)
    {
        string? lastFolder = _settings.ReadSettings().LastFolder;

        string selectedFolder = await SelectAFolder(window, "Select source folder", lastFolder);

        AppSettings appSettings = _settings.ReadSettings();
        appSettings.LastFolder = selectedFolder;
        _settings.Save(appSettings);

        WorkingFolder = selectedFolder;


        return selectedFolder;
    }

    private async Task<string?> SelectTargetFolder(Window window)
    {
        if (WorkingFolder == null)
        {
            return null;
        }
       

        string selectedFolder = await SelectAFolder(window, Resources.Resources.SelectExportFolder, TargetFolder);

        TargetFolder = selectedFolder;

        await using IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(WorkingFolder);

        unitOfWork.ParameterRepository.Update(TargetFolderSettingKey, selectedFolder);

        await unitOfWork.SaveChangesAsync();

        return selectedFolder;
    }

    private static async Task<string> SelectAFolder(Window window, string selectAnExportFolder, string? previousFolder)
    {
        FolderPickerOpenOptions folderPickerOptions = new()
            { AllowMultiple = false, Title = selectAnExportFolder, SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(previousFolder ?? string.Empty) };
        IReadOnlyList<IStorageFolder> folders = await window.StorageProvider.OpenFolderPickerAsync(folderPickerOptions);

        if (folders.Count == 0)
        {
            return previousFolder ?? string.Empty;
        }

        string? tryGetLocalPath = folders[0].TryGetLocalPath();
        if (tryGetLocalPath != null && Directory.Exists(tryGetLocalPath))
        {
            return tryGetLocalPath;
        }

        return previousFolder ?? string.Empty;
    }

    private Func<CanHaveTag, bool> CreateFilterForDone(bool arg) => arg ? item => !item.Done : _ => true;

    private Func<CanHaveTag, bool> CreateFilterForItemFilterInput(string? arg) => !string.IsNullOrWhiteSpace(arg) ? item => item.Item.Contains(arg, StringComparison.OrdinalIgnoreCase) : _ => true;

    private bool IsSelected(Tag x, CanHaveTag? canHaveTag)
    {
        return canHaveTag?.Has(x) ?? false;
    }

    private async Task ScanFolder(CancellationToken cancellationToken)
    {
        _images.Clear();

        if (WorkingFolder == null)
        {
            return;
        }

        await new FolderScan().ScanFolder(cancellationToken, WorkingFolder, _images);
    }

    private async Task PublishToFolder(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(WorkingFolder) || string.IsNullOrWhiteSpace(TargetFolder))
        {
            return;
        }

        await new Publisher().PublishToFolder(cancellationToken, WorkingFolder, TargetFolder);
    }
}
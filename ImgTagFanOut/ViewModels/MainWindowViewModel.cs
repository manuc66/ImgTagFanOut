using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
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
using ImgTagFanOut.Models.Faces;
using ReactiveUI;
using Serilog;

namespace ImgTagFanOut.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private const string TargetFolderSettingKey = "target_folder";
    private const string ShowDoneSettingKey = "show_done";
    private string? _workingFolder;
    private string? _targetFolder;
    private CanHaveTag? _selectedImage;
    private Bitmap? _imageToDisplay;
    private readonly ReadOnlyObservableCollection<CanHaveTag> _filteredImages;
    private string? _tagFilterInput;
    private string? _itemFilterInput;
    private readonly ObservableCollection<Tag> _tagList = new();
    private List<SelectableTag> _filteredTagList;
    private Dictionary<string, int> _personFaceCounts = new(StringComparer.Ordinal);

    private bool _showDone;
    private readonly SourceList<CanHaveTag> _images = new();
    private int _selectedIndex;
    private bool _windowActivated;
    private readonly Settings _settings;
    private string _windowTitle = null!;
    private bool _isBusy;
    private string? _busyMessage;
    private string? _statusMessage;

    internal string? WorkingFolder
    {
        get => _workingFolder;
        set
        {
            this.RaiseAndSetIfChanged(ref _workingFolder, value);
            this.RaisePropertyChanged(nameof(HasWorkingFolder));
        }
    }

    public bool HasWorkingFolder => !string.IsNullOrWhiteSpace(WorkingFolder);

    public string? TargetFolder
    {
        get => _targetFolder;
        set => this.RaiseAndSetIfChanged(ref _targetFolder, value);
    }

    public ReadOnlyObservableCollection<CanHaveTag> FilteredImages => _filteredImages;

    private ObservableCollection<Tag> TagList
    {
        get => _tagList;
        init => this.RaiseAndSetIfChanged(ref _tagList, value);
    }

    public List<SelectableTag> FilteredTagList
    {
        get => _filteredTagList;
        private set => this.RaiseAndSetIfChanged(ref _filteredTagList, value);
    }

    public CanHaveTag? SelectedImage
    {
        get => _selectedImage;
        set => this.RaiseAndSetIfChanged(ref _selectedImage, value);
    }

    public Bitmap? ImageToDisplay
    {
        get => _imageToDisplay;
        private set => this.RaiseAndSetIfChanged(ref _imageToDisplay, value);
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

    public bool ShowDone
    {
        get => _showDone;
        set => this.RaiseAndSetIfChanged(ref _showDone, value);
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedIndex, value);
    }

    public bool WindowActivated
    {
        get => _windowActivated;
        set => this.RaiseAndSetIfChanged(ref _windowActivated, value);
    }

    public string WindowTitle
    {
        get => _windowTitle;
        private set => this.RaiseAndSetIfChanged(ref _windowTitle, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isBusy, value);

            if (value)
            {
                BusyMessage ??= Resources.Resources.PleaseWait;
            }
            else
            {
                BusyMessage = null;
            }
        }
    }

    /// <summary>Progress text shown while a long operation runs.</summary>
    public string? BusyMessage
    {
        get => _busyMessage;
        private set => this.RaiseAndSetIfChanged(ref _busyMessage, value);
    }

    /// <summary>Report of the last operation, shown at the bottom of the window.</summary>
    public string? StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public TagToImagesViewModel TagToImages { get; }

    public FacesViewModel Faces { get; }

    public UnknownFacesViewModel UnknownFaces { get; }

    public ReactiveCommand<Window, string> SelectFolderCommand { get; }
    public ReactiveCommand<Window, string?> SelectTargetFolderCommand { get; }

    public ReactiveCommand<Unit, Unit> ScanFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> EjectDatabaseCommand { get; }
    public ReactiveCommand<Unit, Unit> AnalyzeFacesCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelBusyCommand { get; }
    public ReactiveCommand<Window, Unit> PublishCommand { get; }
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
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowAboutDialogCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowSettingsDialogCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenTargetFolderCommand { get; }

    public Interaction<PublishProgressViewModel, int?> ShowPublishProgressDialog { get; }
    public Interaction<ConsentViewModel, int?> ShowConsentDialog { get; }
    public Interaction<PublishDropOrMergeViewModel, int?> PublishDropOrMergeDialog { get; }
    public Interaction<EjectDatabaseViewModel, bool?> ShowEjectDatabaseDialog { get; }
    public Interaction<AboutViewModel, int?> ShowAboutDialog { get; }
    public Interaction<SettingsViewModel, int?> ShowSettingsDialog { get; }


    public MainWindowViewModel()
        : this(new UnitOfWorkFactory(), new FolderScan(), new HashEvaluator(), new ThumbnailProvider(), new FileManagerHandler()) { }

    internal MainWindowViewModel(
        IUnitOfWorkFactory unitOfWorkFactory,
        IFolderScan folderScan,
        IHashEvaluator hashEvaluator,
        IThumbnailProvider thumbnailProvider,
        IFileManagerHandler fileManagerHandler,
        IFaceScan? faceScan = null)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _folderScan = folderScan;
        _hashEvaluator = hashEvaluator;
        _thumbnailProvider = thumbnailProvider;
        _fileManagerHandler = fileManagerHandler;
        _faceScan = faceScan ?? new FaceScan(unitOfWorkFactory, hashEvaluator);
        TagToImages = new TagToImagesViewModel(() => WorkingFolder, unitOfWorkFactory, thumbnailProvider);
        Faces = new FacesViewModel(unitOfWorkFactory, thumbnailProvider, new PersonSuggester(unitOfWorkFactory));
        Faces.TagsChanged += (_, _) => ReloadTags();

        UnknownFaces = new UnknownFacesViewModel(() => WorkingFolder, unitOfWorkFactory, thumbnailProvider);
        UnknownFaces.TagsChanged += (_, _) => ReloadTags();
        ShowPublishProgressDialog = new();
        ShowConsentDialog = new();
        PublishDropOrMergeDialog = new();
        ShowEjectDatabaseDialog = new();
        ShowAboutDialog = new();
        ShowSettingsDialog = new();
        _settings = new();
        WorkingFolder = "";
        WindowTitle = nameof(ImgTagFanOut);
        TagList = new();
        ImageToDisplay = _noPreviewToDisplay;
        _filteredTagList = new();
        ShowDone = false;
        TagFilterInput = string.Empty;

        _noPreviewToDisplay = LoadNoPreviewToDisplay();

        this.WhenAnyValue(x => x.WindowActivated)
            .DistinctUntilChanged()
            .SelectMany(HandleWindowActivatedAsync)
            .Subscribe();

        this.WhenAnyValue(x => x.ShowDone)
            .SelectMany(SaveShowDownState)
            .Subscribe();

        _images
            .Connect()
            .AutoRefresh(x => x.Done)
            .Filter(this.WhenAnyValue(@this => @this.ShowDone).Select(CreateFilterForDone))
            .Filter(this.WhenValueChanged(@this => @this.ItemFilterInput).Select(CreateFilterForItemFilterInput))
            .Sort(SortExpressionComparer<CanHaveTag>.Ascending(t => t.Item))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _filteredImages)
            .Subscribe();

        this.WhenAnyValue(x => x.WorkingFolder)
            .Subscribe(UpdateWindowTitle);

        this.WhenAnyValue(x => x.TagFilterInput, x => x.SelectedImage)
            .CombineLatest(TagList.ToObservableChangeSet(x => x).ToCollection())
            .Subscribe(watched =>
            {
                var (tagFilterInput, selectedImage) = watched.First;
                var tagList = watched.Second;
                CreateFilteredTagList(tagList, tagFilterInput, selectedImage);
            });

        this.WhenAnyValue(x => x.ImageToDisplay, x => x.SelectedImage)
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Subscribe(DisplayNoPreview);

        this.WhenAnyValue(x => x.SelectedImage)
            .Where(x => !string.IsNullOrWhiteSpace(x?.Item))
            .Buffer(TimeSpan.FromMilliseconds(30))
            .Where(x => x.Count > 0)
            .ObserveOn(RxApp.MainThreadScheduler)
            .SelectMany(FetchThumbnail)
            .Subscribe(UpdateImageDisplay);

        this.WhenAnyValue(x => x.SelectedImage, x => x.WorkingFolder, (image, folder) => (image, folder))
            .Throttle(TimeSpan.FromMilliseconds(80))
            .ObserveOn(RxApp.MainThreadScheduler)
            .SelectMany(pair => Observable.FromAsync(() => Faces.LoadAsync(pair.folder, pair.image)))
            .Subscribe();

        DoneCommand = ReactiveCommand.CreateFromTask(
            ToggleDoneCurrentElement,
            this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null)
        );
        OpenCommand = ReactiveCommand.CreateFromTask(ExecuteOpenFileCommandAsync, this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));
        LocateCommand = ReactiveCommand.CreateFromTask(ExecuteLocateFileCommandAsync, this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));
        OpenTargetFolderCommand =
            ReactiveCommand.CreateFromTask(ExecuteOpenTargetFolderCommandAsync, this.WhenAnyValue(x => x.TargetFolder).Select(x => !string.IsNullOrWhiteSpace(x)));

        ScanFolderCommand = ReactiveCommand.CreateFromTask(
            ExecuteScanFolderCommandAsync,
            this.WhenAnyValue(x => x.WorkingFolder).Select(x => !string.IsNullOrWhiteSpace(x) && Directory.Exists(x))
        );

        EjectDatabaseCommand = ReactiveCommand.CreateFromTask(
            ExecuteEjectDatabaseCommandAsync,
            this.WhenAnyValue(x => x.WorkingFolder, x => x.IsBusy,
                    (folder, isBusy) => !isBusy
                                     && !string.IsNullOrWhiteSpace(folder)
                                     && Directory.Exists(folder)
                                     && _unitOfWorkFactory.DatabaseExists(folder))
                .CombineLatest(ScanFolderCommand.IsExecuting, (canEject, isScanning) => canEject && !isScanning)
        );

        AnalyzeFacesCommand = ReactiveCommand.CreateFromTask(
            ExecuteAnalyzeFacesCommandAsync,
            this.WhenAnyValue(x => x.WorkingFolder, x => x.IsBusy,
                    (folder, isBusy) => !isBusy
                                     && !string.IsNullOrWhiteSpace(folder)
                                     && Directory.Exists(folder))
                .CombineLatest(ScanFolderCommand.IsExecuting, (canAnalyze, isScanning) => canAnalyze && !isScanning)
        );

        CancelBusyCommand = ReactiveCommand.Create(
            () => _busyCancellation?.Cancel(),
            this.WhenAnyValue(x => x.IsBusy)
        );

        SelectFolderCommand = ReactiveCommand.CreateFromTask<Window, string>(ExecuteSelectFolderCommandAsync, ScanFolderCommand.IsExecuting.Select(x => !x));
        SelectFolderCommand.SelectMany(OpenFolder).Subscribe(_ => ScanFolderCommand.Execute().Subscribe());

        SelectTargetFolderCommand = ReactiveCommand.CreateFromTask<Window, string?>(SelectTargetFolder, ScanFolderCommand.IsExecuting.Select(x => !x));

        PublishCommand = ReactiveCommand.CreateFromObservable(
            (Window _) => Observable.StartAsync(PublishToFolder, RxApp.TaskpoolScheduler),
            this.WhenAnyValue(x => x.WorkingFolder).Select(x => !string.IsNullOrWhiteSpace(x) && Directory.Exists(x))
        );

        AllCommand = ReactiveCommand.CreateFromTask(
            ExecuteAssignAllTagsCommandAsync,
            this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null)
        );
        NoneCommand = ReactiveCommand.CreateFromTask(
            ExecuteRemoveAllTagsCommandAsync,
            this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null)
        );

        AddToTagListCommand = ReactiveCommand.CreateFromTask(
            ExecuteAddNewTagCommandAsync,
            this.WhenAnyValue(x => x.TagFilterInput)
                .CombineLatest(
                    TagList.ToObservableChangeSet(x => x).ToCollection().Prepend(new ReadOnlyCollection<Tag>(new List<Tag>())),
                    (tagFilterInput, tagList) => !string.IsNullOrWhiteSpace(tagFilterInput) && !tagList.Any(tag => tag.Same(tagFilterInput))
                )
        );
        ClearTagFilterInputCommand = ReactiveCommand.Create(
            () => { TagFilterInput = string.Empty; },
            this.WhenAnyValue(x => x.TagFilterInput).Select(x => !string.IsNullOrWhiteSpace(x))
        );

        ClearItemFilterInputCommand = ReactiveCommand.Create(
            () => { ItemFilterInput = string.Empty; },
            this.WhenAnyValue(x => x.ItemFilterInput).Select(x => !string.IsNullOrWhiteSpace(x))
        );

        ToggleAssignTagToImageCommand = ReactiveCommand.CreateFromTask((Func<Tag, CancellationToken, Task>)ExecuteToggleAssignTagToImageCommandAsync,
            this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null)
        );

        RemoveTagToImageCommand = ReactiveCommand.CreateFromTask(
            (Func<Tag, CancellationToken, Task>)ExecuteRemoveTagToImageCommandAsync
        );

        DeleteTagCommand = ReactiveCommand.CreateFromTask(
            (Func<SelectableTag, CancellationToken, Task>)ExecuteDeleteTagCommandAsync,
            ScanFolderCommand.IsExecuting.Select(x => !x)
        );

        ExitCommand = ReactiveCommand.CreateFromTask(_ => Task.CompletedTask);
        ShowAboutDialogCommand = ReactiveCommand.CreateFromTask(async _ =>
        {
            AboutViewModel aboutViewModel = new();
            await ShowAboutDialog.Handle(aboutViewModel);
        });
        ShowSettingsDialogCommand = ReactiveCommand.CreateFromTask(async _ =>
        {
            SettingsViewModel settingsViewModel = new();
            await ShowSettingsDialog.Handle(settingsViewModel);
        });
    }


    private void CreateFilteredTagList(IReadOnlyCollection<Tag> list, string? tagFilterInput, CanHaveTag? selectedImage)
    {
        FilteredTagList = list.Where(tag => string.IsNullOrWhiteSpace(tagFilterInput) || tag.MatchFilter(tagFilterInput))
            .Select(tag => new SelectableTag(tag)
            {
                IsSelected = IsSelected(tag, selectedImage),
                IsPerson = _personFaceCounts.ContainsKey(tag.Name),
                PersonFaceCount = _personFaceCounts.GetValueOrDefault(tag.Name),
            })
            .OrderBy(tag => tag.Tag.Name)
            .ToList();
    }


    private async Task ExecuteDeleteTagCommandAsync(SelectableTag s, CancellationToken ct)
    {
        if (WorkingFolder == null)
        {
            return;
        }

        await using (IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(WorkingFolder, ct))
        {
            unitOfWork.TagRepository.DeleteTag(s.Tag);

            TagList.Remove(s.Tag);

            await unitOfWork.SaveChangesAsync(ct);
        }

        foreach (CanHaveTag item in _images.Items)
        {
            item.RemoveTag(s.Tag);
        }
    }


    private async Task ExecuteAnalyzeFacesCommandAsync(CancellationToken cancellationToken)
    {
        string? folder = WorkingFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _busyCancellation = cancellation;
        IsBusy = true;
        BusyMessage = string.Format(Resources.Resources.AnalyzingFaces, 0, 0);
        StatusMessage = null;

        try
        {
            Progress<FaceScanProgress> progress = new(report =>
                BusyMessage = string.Format(Resources.Resources.AnalyzingFaces, report.Processed, report.Total));

            FaceScanSummary summary = await Task.Run(
                () => _faceScan.ScanAsync(folder, progress, cancellation.Token),
                cancellation.Token);

            StatusMessage = summary.AnalyzerUnavailable
                ? Resources.Resources.FaceScanUnavailable
                : string.Format(Resources.Resources.FaceScanSummary, summary.Analyzed, summary.Cached, summary.FaceCount);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Resources.Resources.FaceScanCancelled;
        }
        catch (Exception ex)
        {
            // A locked database or a broken model file must leave the window
            // usable and explain what happened.
            Log.Error(ex, "Face analysis failed for {Folder}", folder);
            StatusMessage = $"{Resources.Resources.FaceScanFailed} {ex.Message}";
        }
        finally
        {
            _busyCancellation = null;
            BusyMessage = null;
            IsBusy = false;
        }
    }

    private async Task ExecuteEjectDatabaseCommandAsync(CancellationToken ct)
    {
        string? folder = WorkingFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder) || !_unitOfWorkFactory.DatabaseExists(folder))
        {
            return;
        }

        bool? confirmed = await ShowEjectDatabaseDialog.Handle(new EjectDatabaseViewModel(folder));
        if (confirmed != true)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            ct.ThrowIfCancellationRequested();
            _unitOfWorkFactory.DeleteDatabase(folder);
            ResetWorkspace();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Resources.Resources.OperationCancelled;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Cannot eject the database of {Folder}", folder);
            StatusMessage = $"{Resources.Resources.OperationFailed} {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }


    private void ResetWorkspace()
    {
        // Clear the working folder first: changing ShowDone afterwards must not recreate the
        // database that was just deleted.
        WorkingFolder = null;
        TargetFolder = null;
        ShowDone = false;
        SelectedImage = null;
        SelectedIndex = 0;
        TagFilterInput = string.Empty;
        ItemFilterInput = string.Empty;
        _images.Clear();
        TagList.Clear();
        TagToImages.Reset();
    }


    private async Task ExecuteRemoveTagToImageCommandAsync(Tag tag, CancellationToken ct)
    {
        if (WorkingFolder == null)
        {
            return;
        }

        if (SelectedImage == null)
        {
            return;
        }

        await using (IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(WorkingFolder, ct))
        {
            unitOfWork.TagRepository.RemoveTagToItem(tag, SelectedImage);

            // Le libellé dit « cette personne est sur la photo » : on retire aussi les visages.
            unitOfWork.FaceRepository.ClearPersonLinks(SelectedImage.Item, tag.Name);

            await unitOfWork.SaveChangesAsync(ct);
        }

        Faces.OnPersonDetached(tag.Name);
        ReloadTags();
        RefreshTagIsSelected();
    }


    private async Task ExecuteToggleAssignTagToImageCommandAsync(Tag s, CancellationToken ct)
    {
        if (WorkingFolder == null)
        {
            return;
        }

        bool removed = SelectedImage?.Has(s) ?? false;
        bool teachesSomething = false;

        await using (IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(WorkingFolder, ct))
        {
            if (SelectedImage != null)
            {
                unitOfWork.TagRepository.ToggleToItem(s, SelectedImage);

                if (removed)
                {
                    unitOfWork.FaceRepository.ClearPersonLinks(SelectedImage.Item, s.Name);
                }
                else if (_personFaceCounts.ContainsKey(s.Name))
                {
                    teachesSomething = unitOfWork.FaceRepository
                        .GetFacesWithPerson(SelectedImage.Item)
                        .Any(row => row.PersonName == s.Name);
                }
            }

            await unitOfWork.SaveChangesAsync(ct);
        }

        if (removed)
        {
            Faces.OnPersonDetached(s.Name);
            ReloadTags();
            StatusMessage = null;
        }
        else if (_personFaceCounts.ContainsKey(s.Name) && !teachesSomething)
        {
            // Le libellé seul ne dit pas quel visage c'est : les suggestions n'apprennent rien.
            StatusMessage = string.Format(Resources.Resources.PersonLabelWithoutFace, s.Name);
        }
        else
        {
            StatusMessage = null;
        }

        RefreshTagIsSelected();
    }


    private async Task ExecuteAddNewTagCommandAsync(CancellationToken ct)
    {
        if (WorkingFolder == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(TagFilterInput))
        {
            return;
        }

        await using IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(WorkingFolder, ct);
        if (unitOfWork.TagRepository.TryCreateTag(TagFilterInput, out Tag? newTag))
        {
            TagList.Add(newTag);
        }

        await unitOfWork.SaveChangesAsync(ct);

        TagFilterInput = string.Empty;
    }


    private async Task ExecuteRemoveAllTagsCommandAsync(CancellationToken ct)
    {
        if (WorkingFolder == null)
        {
            return;
        }

        if (SelectedImage == null)
        {
            return;
        }

        await using (IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(WorkingFolder, ct))
        {
            List<string> removed = SelectedImage.Tags.Select(tag => tag.Name).ToList();

            foreach (Tag selectedImageTag in SelectedImage.Tags.ToImmutableList())
            {
                unitOfWork.TagRepository.RemoveTagToItem(selectedImageTag, SelectedImage);
            }

            unitOfWork.FaceRepository.ClearAllPersonLinks(SelectedImage.Item);

            await unitOfWork.SaveChangesAsync(ct);

            // Après la sauvegarde : la liste des personnes est relue depuis la base.
            foreach (string name in removed)
            {
                Faces.OnPersonDetached(name);
            }

            ReloadTags();
        }

        RefreshTagIsSelected();
    }


    private async Task ExecuteAssignAllTagsCommandAsync(CancellationToken ct)
    {
        if (SelectedImage == null)
        {
            return;
        }

        await AssignAllTags(SelectedImage, TagList, ct);
    }


    private void UpdateWindowTitle(string? x)
    {
        WindowTitle = string.IsNullOrWhiteSpace(x) ? $"{nameof(ImgTagFanOut)}" : $"{nameof(ImgTagFanOut)} - {x}";
    }


    private async Task ExecuteScanFolderCommandAsync(CancellationToken cts)
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            await Task.Run(async () => await ScanFolder(cts), cts);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Resources.Resources.OperationCancelled;
        }
        catch (Exception ex)
        {
            // A removed USB key or an unreadable database used to vanish into
            // the global exception handler; show it where the user looks.
            Log.Error(ex, "Folder scan failed for {Folder}", WorkingFolder);
            StatusMessage = $"{Resources.Resources.ScanFailed} {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }


    private async Task ToggleDoneCurrentElement(CancellationToken ct)
    {
        if (SelectedImage == null || WorkingFolder == null)
        {
            return;
        }

        int selectedIndex = SelectedIndex;
        await using (IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(WorkingFolder, ct))
        {
            if (!SelectedImage.Done)
            {
                unitOfWork.TagRepository.MarkDone(SelectedImage);
            }
            else
            {
                unitOfWork.TagRepository.MarkUnDone(SelectedImage);
            }

            await unitOfWork.SaveChangesAsync(ct);
        }

        if (ShowDone)
        {
            selectedIndex++;
        }

        SelectedIndex = Math.Min(selectedIndex, FilteredImages.Count - 1);
    }


    private async Task<bool> SaveShowDownState(bool x)
    {
        if (string.IsNullOrEmpty(WorkingFolder) || !Directory.Exists(WorkingFolder))
        {
            return x;
        }

        try
        {
            await using IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(WorkingFolder);
            unitOfWork.ParameterRepository.Update(ShowDoneSettingKey, x.ToString());
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // This preference is only a convenience: losing it must not crash
            // the activation or the scan that follows.
            Log.Warning(ex, "Cannot save the ShowDone preference");
        }

        return x;
    }


    private async Task<bool> HandleWindowActivatedAsync(bool x)
    {
        if (!x) return x;

        Settings settings = new();
        AppSettings readSettings = settings.ReadSettings();
        if (readSettings.ErrorTrackingAllowed == null)
        {
            ConsentViewModel consentViewModel = new();
            await ShowConsentDialog.Handle(consentViewModel);
        }

        return x;
    }


    private void UpdateImageDisplay((CanHaveTag?, Bitmap? thumbnail) x)
    {
        if (SelectedImage != x.Item1)
        {
            x.Item2?.Dispose();
            return;
        }

        Bitmap? previous = ImageToDisplay;

        ImageToDisplay = x.Item2 ?? _noPreviewToDisplay;

        if (previous != _noPreviewToDisplay)
        {
            previous?.Dispose();
        }
    }


    private void DisplayNoPreview((Bitmap? thumbnail, CanHaveTag?) x)
    {
        if (x is { Item1: not null, Item2: not null }) return;
        Bitmap? previous = ImageToDisplay;

        ImageToDisplay = _noPreviewToDisplay;

        if (previous != _noPreviewToDisplay)
        {
            previous?.Dispose();
        }
    }


    private async Task<(CanHaveTag?, Bitmap? thumbnail)> FetchThumbnail(IList<CanHaveTag?> x)
    {
        CanHaveTag? canHaveTag = x.LastOrDefault();
        if (WorkingFolder == null || canHaveTag == null)
        {
            return (null, null);
        }

        string fullFilePath = canHaveTag.GetFullFilePath(WorkingFolder);
        if (File.Exists(fullFilePath))
        {
            SearchForTagBasedOnFileHash(canHaveTag);

            Bitmap? thumbnail = await _thumbnailProvider.GetThumbnail(fullFilePath);

            return ((CanHaveTag?)canHaveTag, thumbnail);
        }
        else
        {
            return ((CanHaveTag?)canHaveTag, null);
        }
    }


    private CancellationTokenSource _currentHashLookup = new();
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IFolderScan _folderScan;
    private readonly IHashEvaluator _hashEvaluator;
    private readonly IThumbnailProvider _thumbnailProvider;
    private readonly IFileManagerHandler _fileManagerHandler;
    private readonly IFaceScan _faceScan;
    private CancellationTokenSource? _busyCancellation;
    private readonly Bitmap _noPreviewToDisplay;


    private void SearchForTagBasedOnFileHash(CanHaveTag canHaveTag)
    {
        _currentHashLookup.Cancel();
        _currentHashLookup.Dispose();
        _currentHashLookup = new();
        RxApp.MainThreadScheduler.ScheduleAsync(
            async (_, ct) =>
            {
                try
                {
                    using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct, _currentHashLookup.Token);
                    if (WorkingFolder == null || cts.IsCancellationRequested)
                    {
                        return;
                    }

                    CancellationToken cancellationToken = cts.Token;
                    canHaveTag.Hash = await _hashEvaluator.ComputeHashAsync(canHaveTag.GetFullFilePath(WorkingFolder), cancellationToken);
                    if (canHaveTag != SelectedImage || canHaveTag.Hash == null || SelectedImage.Done || cts.IsCancellationRequested)
                    {
                        return;
                    }

                    ImmutableList<Tag> allTagForHash;
                    await using (IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(WorkingFolder, cancellationToken))
                    {
                        allTagForHash = unitOfWork.TagRepository.GetAllTagForHash(canHaveTag.Hash);
                    }

                    if (canHaveTag != SelectedImage || cts.IsCancellationRequested)
                    {
                        return;
                    }

                    await AssignAllTags(SelectedImage, allTagForHash, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Another photo was selected, or the window is closing.
                }
                catch (Exception ex)
                {
                    // The photo may have been deleted or renamed meanwhile.
                    Log.Warning(ex, "Cannot look up tags by hash for {File}", canHaveTag.Item);
                }
            }
        );
    }


    internal async Task<string> OpenFolder(string path)
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            await using IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(path);

            TargetFolder = unitOfWork.ParameterRepository.Get(TargetFolderSettingKey);
            ShowDone = bool.TryParse(unitOfWork.ParameterRepository.Get(ShowDoneSettingKey), out bool showDone) && showDone;

            ReloadTagList(unitOfWork);
            TagToImages.LoadTags();

            return path;
        }
        catch (Exception ex)
        {
            // Opening a folder that disappeared (removable drive) must not
            // leave a busy cursor or an unobserved exception behind.
            Log.Error(ex, "Cannot open the folder {Folder}", path);
            StatusMessage = $"{Resources.Resources.OperationFailed} {ex.Message}";
            return path;
        }
        finally
        {
            IsBusy = false;
        }
    }


    private void RefreshTagIsSelected()
    {
        foreach (SelectableTag selectableTag in FilteredTagList)
        {
            selectableTag.IsSelected = IsSelected(selectableTag.Tag, SelectedImage);
        }
    }


    private async Task AssignAllTags(CanHaveTag selectedImage, IEnumerable<Tag> allTags, CancellationToken cancellationToken)
    {
        if (WorkingFolder == null)
        {
            return;
        }

        await using (IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(WorkingFolder, cancellationToken))
        {
            foreach (Tag selectedImageTag in allTags)
            {
                unitOfWork.TagRepository.AddTagToItem(selectedImageTag, selectedImage);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        RefreshTagIsSelected();
    }

    private async Task ExecuteOpenFileCommandAsync()
    {
        if (SelectedImage == null || WorkingFolder == null)
        {
            return;
        }

        string path = Path.Combine(WorkingFolder, SelectedImage.Item);

        if (File.Exists(path))
        {
            await _fileManagerHandler.OpenFile(path);
        }
    }


    private async Task ExecuteOpenTargetFolderCommandAsync()
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


    private async Task ExecuteLocateFileCommandAsync()
    {
        if (SelectedImage == null || WorkingFolder == null)
        {
            return;
        }

        string path = Path.Combine(WorkingFolder, SelectedImage.Item);

        if (File.Exists(path))
        {
            await _fileManagerHandler.RevealFileInFolder(path);
        }
    }


    private static Bitmap LoadNoPreviewToDisplay()
    {
        string resourceName = "ImgTagFanOut.NoPreview.png";
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName) ?? throw new Exception("Resource not found: " + resourceName);
        Bitmap noPreviewToDisplay = new(stream);
        return noPreviewToDisplay;
    }


    /// <summary>
    /// Recharges le panneau des libellés : nommer un visage crée ou assigne un tag,
    /// qui doit apparaître comme les autres, y compris dans l'onglet Libellés.
    /// </summary>
    internal void ReloadTags()
    {
        if (string.IsNullOrWhiteSpace(WorkingFolder) || !_unitOfWorkFactory.DatabaseExists(WorkingFolder))
        {
            return;
        }

        try
        {
            using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(WorkingFolder);
            ReloadTagList(unitOfWork);
            TagToImages.LoadTags();
        }
        catch (Exception ex)
        {
            // Called from UI events (naming a face); a database hiccup must not
            // take the window down.
            Log.Warning(ex, "Cannot reload the tags of {Folder}", WorkingFolder);
        }
    }


    private void ReloadTagList(IUnitOfWork unitOfWork)
    {
        // Avant de vider la liste : le changement déclenche la reconstruction des
        // entrées, qui a besoin de savoir quels libellés sont des personnes.
        _personFaceCounts = new Dictionary<string, int>(
            unitOfWork.FaceRepository.GetPersonFaceCounts(),
            StringComparer.Ordinal);

        _unitOfWorkFactory.ClearTagCache();
        TagList.Clear();
        TagList.AddRange(unitOfWork.TagRepository.GetAllTag());
    }


    private async Task<string> ExecuteSelectFolderCommandAsync(Window window)
    {
        string? lastFolder = _settings.ReadSettings().LastFolder;
        if (string.IsNullOrEmpty(lastFolder))
        {
            lastFolder = EnvironmentService.GetMyPictureFolder();
        }

        string selectedFolder = await SelectAFolder(window, Resources.Resources.SelectWorkingFolder, lastFolder);

        if (WorkingFolder != selectedFolder)
        {
            WorkingFolder = null;
            AppSettings appSettings = _settings.ReadSettings();
            appSettings.LastFolder = selectedFolder;
            _settings.Save(appSettings);

            WorkingFolder = selectedFolder;
        }

        return selectedFolder;
    }


    private async Task<string?> SelectTargetFolder(Window window)
    {
        if (WorkingFolder == null)
        {
            return null;
        }

        IsBusy = true;
        try
        {
            string selectedFolder = await SelectAFolder(window, Resources.Resources.SelectExportFolder, TargetFolder);

            TargetFolder = selectedFolder;

            await using IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(WorkingFolder);

            unitOfWork.ParameterRepository.Update(TargetFolderSettingKey, selectedFolder);

            await unitOfWork.SaveChangesAsync();

            return selectedFolder;
        }
        finally
        {
            IsBusy = false;
        }
    }


    private static async Task<string> SelectAFolder(Window window, string selectAnExportFolder, string? previousFolder)
    {
        FolderPickerOpenOptions folderPickerOptions = new()
        {
            AllowMultiple = false,
            Title = selectAnExportFolder,
            SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(previousFolder ?? string.Empty),
        };
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


    private static Func<CanHaveTag, bool> CreateFilterForDone(bool arg) => arg ? _ => true : item => !item.Done;


    private static Func<CanHaveTag, bool> CreateFilterForItemFilterInput(string? arg) =>
        !string.IsNullOrWhiteSpace(arg) ? item => item.Item.Contains(arg, StringComparison.OrdinalIgnoreCase) : _ => true;


    private static bool IsSelected(Tag x, CanHaveTag? canHaveTag)
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

        await _folderScan.ScanFolder(cancellationToken, WorkingFolder, _images);

        await using IUnitOfWork unitOfWork = await _unitOfWorkFactory.GetUnitOfWorkAsync(WorkingFolder, cancellationToken);
        ReloadTagList(unitOfWork);
        TagToImages.LoadTags();
    }


    private async Task PublishToFolder(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(WorkingFolder) || string.IsNullOrWhiteSpace(TargetFolder))
        {
            return;
        }

        bool? dropEverythingFirst;
        if (Directory.Exists(TargetFolder) && Directory.GetDirectories(TargetFolder).Length > 0)
        {
            PublishDropOrMergeViewModel publishDropOrMergeViewModel = new();
            await PublishDropOrMergeDialog.Handle(publishDropOrMergeViewModel);

            if (!publishDropOrMergeViewModel.Merge.HasValue)
            {
                dropEverythingFirst = null;
            }
            else
            {
                dropEverythingFirst = !publishDropOrMergeViewModel.Merge;
            }
        }
        else
        {
            dropEverythingFirst = false;
        }

        if (!dropEverythingFirst.HasValue)
        {
            return;
        }

        PublishProgressViewModel viewModel = new(WorkingFolder, TargetFolder, dropEverythingFirst.Value, _settings.ReadSettings().LinkMode, cancellationToken);

        await ShowPublishProgressDialog.Handle(viewModel);
    }
}
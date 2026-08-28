using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using DynamicData;
using DynamicData.Binding;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

public class TagToImagesViewModel : ViewModelBase
{
    private readonly Func<string?> _workingFolderGetter;
    private readonly ReadOnlyObservableCollection<CanHaveTag> _filteredImages;
    private readonly SourceList<CanHaveTag> _images = new();
    private Bitmap? _imageToDisplay;
    private CanHaveTag? _selectedImage;

    public TagToImagesViewModel(Func<string?> workingFolderGetter)
    {
        _workingFolderGetter = workingFolderGetter;
        _images
            .Connect()
            .Filter(this.WhenAnyValue(@this => @this.ItemFilterInput).Select(CreateFilterForItemFilterInput))
            .Sort(SortExpressionComparer<CanHaveTag>.Ascending(t => t.Item))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _filteredImages)
            .Subscribe();

        this.WhenAnyValue(x => x.SelectedTag)
            .Subscribe(async _ => await LoadImagesForSelectedTagAsync());

        this.WhenAnyValue(x => x.SelectedImage)
            .Where(x => x != null)
            .SelectMany(LoadThumbnailAsync)
            .Subscribe(UpdateImageDisplay);
    }

    public ObservableCollection<Tag> TagList { get; } = new();

    private Tag? _selectedTag;
    public Tag? SelectedTag
    {
        get => _selectedTag;
        set => this.RaiseAndSetIfChanged(ref _selectedTag, value);
    }

    private string? _itemFilterInput;
    public string? ItemFilterInput
    {
        get => _itemFilterInput;
        set => this.RaiseAndSetIfChanged(ref _itemFilterInput, value);
    }

    public ReadOnlyObservableCollection<CanHaveTag> FilteredImages => _filteredImages;

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

    public void LoadTags()
    {
        string? workingFolder = _workingFolderGetter();
        if (workingFolder == null)
        {
            return;
        }

        using IUnitOfWork unitOfWork = DbContextFactory.GetUnitOfWork(workingFolder);
        TagList.Clear();
        TagList.AddRange(unitOfWork.TagRepository.GetAllTag());
    }

    private async Task LoadImagesForSelectedTagAsync()
    {
        _images.Clear();
        ImageToDisplay = null;
        SelectedImage = null;

        string? workingFolder = _workingFolderGetter();
        if (workingFolder == null || SelectedTag == null)
        {
            return;
        }

        await using IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(workingFolder);
        IReadOnlyList<string> items = unitOfWork.TagRepository.GetItemsWithTag(SelectedTag);

        foreach (string item in items)
        {
            _images.Add(new CanHaveTag(item));
        }
    }

    private async Task<(CanHaveTag?, Bitmap? thumbnail)> LoadThumbnailAsync(CanHaveTag? canHaveTag)
    {
        string? workingFolder = _workingFolderGetter();
        if (workingFolder == null || canHaveTag == null)
        {
            return (null, null);
        }

        string fullFilePath = canHaveTag.GetFullFilePath(workingFolder);
        if (!File.Exists(fullFilePath))
        {
            return (canHaveTag, null);
        }

        Bitmap? thumbnail = await new ThumbnailProvider().GetThumbnail(fullFilePath);
        return (canHaveTag, thumbnail);
    }

    private void UpdateImageDisplay((CanHaveTag?, Bitmap? thumbnail) x)
    {
        if (SelectedImage != x.Item1)
        {
            x.Item2?.Dispose();
            return;
        }

        Bitmap? previous = ImageToDisplay;
        ImageToDisplay = x.Item2;
        previous?.Dispose();
    }

    private static Func<CanHaveTag, bool> CreateFilterForItemFilterInput(string? arg) =>
        !string.IsNullOrWhiteSpace(arg) ? item => item.Item.Contains(arg, StringComparison.OrdinalIgnoreCase) : _ => true;
}
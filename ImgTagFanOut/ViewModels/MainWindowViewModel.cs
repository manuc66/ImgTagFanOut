using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
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

public class Tag : IEquatable<Tag>
{
    public string Name { get; }

    public Tag(string name)
    {
        Name = name;
    }

    public bool Equals(Tag? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Tag)obj);
    }

    private sealed class NameEqualityComparer : IEqualityComparer<Tag>
    {
        public bool Equals(Tag x, Tag y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.Equals(y);
        }

        public int GetHashCode(Tag obj)
        {
            return obj.GetHashCode();
        }
    }

    public static IEqualityComparer<Tag> Comparer { get; } = new NameEqualityComparer();

    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
    }

    public bool Same(string? s)
    {
        return s != null && string.Equals(Name, s, StringComparison.OrdinalIgnoreCase);
    }

    public bool MatchFilter(string? tagFilterInput)
    {
        return tagFilterInput != null && Name.Contains(tagFilterInput, StringComparison.OrdinalIgnoreCase);
    }
}

public class CanHaveTag<T> : ViewModelBase
{
    private T _item;
    private ObservableCollection<Tag> _tags = new();

    public ObservableCollection<Tag> Tags
    {
        get => _tags;
        set => this.RaiseAndSetIfChanged(ref _tags, value);
    }

    public T Item
    {
        get => _item;
    }

    public CanHaveTag(T item)
    {
        _item = item;
    }

    public void AddTag(Tag tag)
    {
        if (!Has(tag))
        {
            _tags.Add(tag);
        }
    }

    public void RemoveTag(Tag tag)
    {
        if (Has(tag))
        {
            _tags.Remove(tag);
        }
    }

    public bool Has(Tag tag)
    {
        return _tags.Any(x => x.Equals(tag));
    }

    public void Toggle(Tag tag)
    {
        if (Has(tag))
        {
            _tags.Remove(tag);
        }
        else
        {
            _tags.Add(tag);
        }
    }
}

class TagRepository
{
    private readonly HashSet<Tag> _tags = new HashSet<Tag>(Tag.Comparer);

    public bool TryCreateTag(string? tagName, [MaybeNullWhen(false)] out Tag newTag)
    {
        if (!string.IsNullOrWhiteSpace(tagName?.Trim()))
        {
            newTag = new Tag(tagName.Trim());
            bool added = _tags.Add(newTag);
            if (added)
            {
                return true;
            }
        }

        newTag = null;
        return false;
    }

    public ImmutableList<Tag> GetAll()
    {
        return _tags.ToImmutableList();
    }

    public void AddTagToItem<T>(string tagName, CanHaveTag<T> tagAssignation)
    {
        if (_tags.TryGetValue(new Tag(tagName), out Tag? existingTag))
        {
            tagAssignation.AddTag(existingTag);
        }
    }

    public void RemoveTagToItem<T>(string tagName, CanHaveTag<T>? tagAssignation)
    {
        if (_tags.TryGetValue(new Tag(tagName), out Tag? existingTag))
        {
            tagAssignation?.RemoveTag(existingTag);
        }
    }

    public void ToggleToItem(string tagName, CanHaveTag<string> tagAssignation)
    {
        ToggleToItem(new Tag(tagName), tagAssignation);
    }

    public void ToggleToItem(Tag tagName, CanHaveTag<string> tagAssignation)
    {
        if (_tags.TryGetValue(tagName, out Tag? existingTag))
        {
            tagAssignation.Toggle(existingTag);
        }
    }
}

public class SelectableTag : ViewModelBase
{
    private Tag _tag;
    private bool _isSelected;

    public Tag Tag
    {
        get => _tag;
        set => this.RaiseAndSetIfChanged(ref _tag, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    public SelectableTag(Tag tag)
    {
        Tag = tag;
    }
}

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
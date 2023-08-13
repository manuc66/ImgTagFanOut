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

    public CanHaveTag(T item)
    {
        this._item = item;
    }

    public void AddTag(Tag tag)
    {
        if (!_tags.Any(x => x.Equals(tag)))
        {
            _tags.Add(tag);
        }
    }

    public void RemoveTag(Tag tag)
    {
        if (_tags.Any(x => x.Equals(tag)))
        {
            _tags.Remove(tag);
        }
    }

    public void Toggle(Tag tag)
    {
        if (!_tags.Any(x => x.Equals(tag)))
        {
            _tags.Add(tag);
        }
        else
        {
            _tags.Remove(tag);
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
            _tags.Add(newTag);
            return true;
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

    public void RemoveTagToItem<T>(string tagName, CanHaveTag<T> tagAssignation)
    {
        if (_tags.TryGetValue(new Tag(tagName), out Tag? existingTag))
        {
            tagAssignation.RemoveTag(existingTag);
        }
    }

    public void ToggleToItem(string tagName, CanHaveTag<string> tagAssignation)
    {
        if (_tags.TryGetValue(new Tag(tagName), out Tag? existingTag))
        {
            tagAssignation.Toggle(existingTag);
        }
    }
}

public class MainWindowViewModel : ViewModelBase
{
    private string _workingFolder = string.Empty;
    private string _selectedImage = string.Empty;
    private Bitmap? _imageToDisplay = null;
    private ObservableCollection<string> _images = new();
    private string? _tagFilterInput;
    private ObservableCollection<string> _tagList = new();
    private List<string> _filteredTagList;
    private readonly TagRepository _tagRepository = new();
    private CanHaveTag<string>? _selectedImageTag;

    public string WorkingFolder
    {
        get => _workingFolder;
        set => this.RaiseAndSetIfChanged(ref _workingFolder, value);
    }

    public ReactiveCommand<Window, string> SelectFolderCommand { get; }

    public ReactiveCommand<Unit, Unit> ScanFolderCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelScanCommand { get; }

    public ReactiveCommand<String, Unit> ToggleAssignTagToImageCommand { get; }

    public ReactiveCommand<Tag, Unit> RemoveTagToImageCommand { get; }

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

    public CanHaveTag<string>? SelectedImageTag
    {
        get => _selectedImageTag;
        set => this.RaiseAndSetIfChanged(ref _selectedImageTag, value);
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
        _tagRepository.TryCreateTag("Lucas", out _);
        _tagRepository.TryCreateTag("Louis", out _);
        _tagRepository.TryCreateTag("Filip", out _);
        TagList = new ObservableCollection<string>(_tagRepository.GetAll().Select(x => x.Name));
        _filteredTagList = new List<string>();

        ScanFolderCommand = ReactiveCommand.CreateFromObservable(
            () => Observable
                .StartAsync(ScanFolder)
                .TakeUntil(CancelScanCommand!));
        ScanFolderCommand.Subscribe(x => { });
        SelectFolderCommand = ReactiveCommand.CreateFromTask<Window, string>(async window =>
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
            if (_tagRepository.TryCreateTag(TagFilterInput, out Tag? newTag))
            {
                TagList.Add(newTag.Name);
            }
        }, this.WhenAnyValue(x => x.TagFilterInput)
            .Select(x => !string.IsNullOrWhiteSpace(x) && !TagList.Any(tag => tag.Equals(x.Trim(), StringComparison.OrdinalIgnoreCase)))
            .CombineLatest(TagList
                .ToObservableChangeSet(x => x)
                .ToCollection()
                .Select(collection => !collection.Any(tag => tag.Equals(TagFilterInput, StringComparison.OrdinalIgnoreCase))), (b, b1) => b && b1));
        ClearTagFilterInputCommand = ReactiveCommand.Create(() => { TagFilterInput = String.Empty; },
            this.WhenAnyValue(x => x.TagFilterInput).Select(x => !string.IsNullOrWhiteSpace(x)));

        ToggleAssignTagToImageCommand = ReactiveCommand.Create(
            (String s) =>
            {
                if (SelectedImageTag != null)
                {
                    _tagRepository.ToggleToItem(s, SelectedImageTag);
                }
            });

        RemoveTagToImageCommand = ReactiveCommand.Create(
            (Tag s) => { _tagRepository.RemoveTagToItem(s.Name, SelectedImageTag); });

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
            .ToObservableChangeSet(x => x)
            .ToCollection()
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

                SelectedImageTag = new CanHaveTag<string>(fullFilePath);

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
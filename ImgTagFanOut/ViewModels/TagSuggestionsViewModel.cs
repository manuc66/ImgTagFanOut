using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ReactiveUI;
using Serilog;

namespace ImgTagFanOut.ViewModels;

/// <summary>
/// The strip of tags the local engine proposes for the selected image. A
/// suggestion is only a proposal: nothing is assigned until the user accepts.
/// </summary>
public class TagSuggestionsViewModel : ViewModelBase
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private CanHaveTag? _image;
    private string? _folder;
    private int _loadVersion;

    public TagSuggestionsViewModel(IUnitOfWorkFactory unitOfWorkFactory)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
    }

    /// <summary>Design-time previewer only, like the other view models.</summary>
    public TagSuggestionsViewModel()
        : this(new UnitOfWorkFactory()) { }

    public ObservableCollection<TagSuggestionItemViewModel> Items { get; } = new();

    public CanHaveTag? Image => _image;

    public string? Folder => _folder;

    public bool HasSuggestions => Items.Count > 0;

    /// <summary>Raised when a suggestion was accepted, so the tag panel can follow.</summary>
    public event EventHandler? TagsChanged;

    /// <summary>Refreshes the strip for the given image. Runs on the caller's thread.</summary>
    public async Task LoadAsync(string? folder, CanHaveTag? image)
    {
        int version = ++_loadVersion;

        _image = image;
        _folder = folder;
        Items.Clear();
        RaiseCount();

        if (string.IsNullOrWhiteSpace(folder) || image is null)
        {
            RaiseCount();
            return;
        }

        try
        {
            IReadOnlyList<TagSuggestionDao> rows = await Task.Run(() => LoadRows(folder, image.Item));

            // A newer selection won while the database was read.
            if (version != _loadVersion)
            {
                return;
            }

            foreach (TagSuggestionDao row in rows)
            {
                Items.Add(new TagSuggestionItemViewModel(row, AcceptAsync, RejectAsync));
            }
        }
        catch (Exception exception)
        {
            // Selecting a photo is a background action: a locked database must
            // leave the strip empty, not take the window down.
            Log.Warning(exception, "Cannot load the tag suggestions of {Item}", image.Item);
            if (version == _loadVersion)
            {
                Items.Clear();
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                RaiseCount();
            }
        }
    }

    private IReadOnlyList<TagSuggestionDao> LoadRows(string folder, string itemName)
    {
        using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(folder);

        HashSet<string> vocabulary = unitOfWork.TagRepository
            .GetAllTag()
            .Select(tag => tag.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return unitOfWork.SuggestionRepository
            .GetSuggestions(itemName)
            .Where(suggestion => suggestion.State == TagSuggestionState.Pending)
            .Where(suggestion => vocabulary.Contains(suggestion.TagName))
            .ToList();
    }

    private async Task AcceptAsync(TagSuggestionItemViewModel item)
    {
        if (_folder is null || _image is null)
        {
            return;
        }

        string folder = _folder;
        CanHaveTag image = _image;

        bool assigned = await Task.Run(() =>
        {
            using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(folder);

            TagDao? tag = unitOfWork.TagRepository.GetTagDao(item.TagName);
            if (tag is null)
            {
                // The tag was deleted between the scan and the click.
                return false;
            }

            unitOfWork.TagRepository.AddTagToItem(new Tag(tag.Name), image);
            unitOfWork.SuggestionRepository.SetState(image.Item, tag.Name, TagSuggestionState.Accepted);
            unitOfWork.SaveChanges();
            return true;
        });

        Items.Remove(item);
        RaiseCount();

        if (!assigned)
        {
            return;
        }

        if (!image.Has(new Tag(item.TagName)))
        {
            image.AddTag(new Tag(item.TagName));
        }

        TagsChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task RejectAsync(TagSuggestionItemViewModel item)
    {
        if (_folder is null || _image is null)
        {
            return;
        }

        string folder = _folder;
        string itemName = _image.Item;

        await Task.Run(() =>
        {
            using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(folder);
            unitOfWork.SuggestionRepository.SetState(itemName, item.TagName, TagSuggestionState.Rejected);
            unitOfWork.SaveChanges();
        });

        Items.Remove(item);
        RaiseCount();
    }

    private void RaiseCount()
    {
        this.RaisePropertyChanged(nameof(HasSuggestions));
    }
}

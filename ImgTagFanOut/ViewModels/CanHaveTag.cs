using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

public class CanHaveTag : ViewModelBase
{
    private readonly string _item;
    private HashSet<Tag> _tagSet = new();
    private ObservableCollection<Tag> _tags = new();
    private bool _done;

    public string? Hash { get; set; }

    public ObservableCollection<Tag> Tags
    {
        get => _tags;
        set => this.RaiseAndSetIfChanged(ref _tags, value);
    }
    
    public bool Done
    {
        get => _done;
        set => this.RaiseAndSetIfChanged(ref _done, value);
    }

    public string Item => _item;

    public CanHaveTag(string item)
    {
        _item = item;
    }

    public void AddTag(Tag tag)
    {
        if (!Has(tag))
        {
            _tagSet.Add(tag);
            _tags.Add(tag);
        }
    }

    public void RemoveTag(Tag tag)
    {
        if (Has(tag))
        {
            _tagSet.Remove(tag);
            _tags.Remove(tag);
        }
    }

    public bool Has(Tag tag)
    {
        return _tagSet.Contains(tag);
    }

    public void Toggle(Tag tag)
    {
        if (Has(tag))
        {
            _tags.Remove(tag);
            _tagSet.Remove(tag);
        }
        else
        {
            _tagSet.Add(tag);
            _tags.Add(tag);
        }
    }
}
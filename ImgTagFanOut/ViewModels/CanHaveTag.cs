using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

public class CanHaveTag : ViewModelBase
{
    private readonly string _item;
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
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

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
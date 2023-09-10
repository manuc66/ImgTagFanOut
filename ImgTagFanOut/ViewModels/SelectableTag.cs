using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

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
        _tag = tag;
    }
}
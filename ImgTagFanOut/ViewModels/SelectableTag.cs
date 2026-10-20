using System;
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

    /// <summary>Vrai quand ce libellé nomme une personne : il peut aussi être lié à un visage.</summary>
    public bool IsPerson { get; init; }

    /// <summary>Infobulle expliquant la double portée d'un libellé de personne.</summary>
    public string? PersonHint => IsPerson ? Resources.Resources.PersonTagHint : null;

    public SelectableTag(Tag tag)
    {
        _tag = tag;
    }
}

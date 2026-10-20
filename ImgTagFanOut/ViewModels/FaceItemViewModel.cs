using Avalonia.Media;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

/// <summary>One face of the strip: its crop, the person it has been named after, and a guess.</summary>
public class FaceItemViewModel : ViewModelBase
{
    private string? _person;

    public FaceItemViewModel(
        int faceId,
        string? person,
        IImage? image,
        string? suggestedPerson = null,
        float suggestedScore = 0)
    {
        FaceId = faceId;
        _person = person;
        Image = image;
        SuggestedPerson = suggestedPerson;
        SuggestedScore = suggestedScore;
    }

    public int FaceId { get; }

    /// <summary>Crop of the face, or null when the image could not be decoded.</summary>
    public IImage? Image { get; }

    public string? Person
    {
        get => _person;
        set
        {
            this.RaiseAndSetIfChanged(ref _person, value);
            this.RaisePropertyChanged(nameof(Label));
            this.RaisePropertyChanged(nameof(SuggestionLabel));
        }
    }

    /// <summary>Best guess for an unnamed face, null when nothing is close enough.</summary>
    public string? SuggestedPerson { get; }

    public float SuggestedScore { get; }

    public string Label => Person ?? "?";

    /// <summary>Shown under an unnamed face, e.g. "≈ Maman".</summary>
    public string SuggestionLabel =>
        Person is null && SuggestedPerson is not null
            ? $"≈ {SuggestedPerson} ({SuggestedScore:0.00})"
            : string.Empty;
}

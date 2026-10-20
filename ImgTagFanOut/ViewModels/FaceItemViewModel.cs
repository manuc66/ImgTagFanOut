using Avalonia.Media;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

/// <summary>One face of the strip: its crop and the person it has been named after.</summary>
public class FaceItemViewModel : ViewModelBase
{
    private string? _person;

    public FaceItemViewModel(int faceId, string? person, IImage? image)
    {
        FaceId = faceId;
        _person = person;
        Image = image;
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
            this.RaisePropertyChanged(nameof(IsUnknown));
        }
    }

    public string Label => Person ?? "?";

    public bool IsUnknown => Person is null;
}

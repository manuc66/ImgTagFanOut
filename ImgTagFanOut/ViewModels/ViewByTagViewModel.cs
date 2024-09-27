using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

public class ViewByTagViewModel : ViewModelBase
{
    private Bitmap? _imageToDisplay;
    public Bitmap? ImageToDisplay
    {
        get => _imageToDisplay;
        set => this.RaiseAndSetIfChanged(ref _imageToDisplay, value);
    }
    public ReactiveCommand<Unit, Unit> OpenCommand { get; }
    public ReactiveCommand<Unit, Unit> LocateCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearTagFilterInputCommand { get; }
    
    private CanHaveTag? _selectedImage;
    public CanHaveTag? SelectedImage
    {
        get => _selectedImage;
        set => this.RaiseAndSetIfChanged(ref _selectedImage, value);
    }
    private string? _workingFolder;
    public string? WorkingFolder
    {
        get => _workingFolder;
        set => this.RaiseAndSetIfChanged(ref _workingFolder, value);
    }
    private bool _isBusy = false;
    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }
    private List<SelectableTag> _filteredTagList;
    public List<SelectableTag> FilteredTagList
    {
        get => _filteredTagList;
        set => this.RaiseAndSetIfChanged(ref _filteredTagList, value);
    }
    
    private string? _tagFilterInput;
    public string? TagFilterInput
    {
        get => _tagFilterInput;
        set => this.RaiseAndSetIfChanged(ref _tagFilterInput, value);
    }

    public ViewByTagViewModel()
    {
        _filteredTagList = new List<SelectableTag>();
        OpenCommand =
            ReactiveCommand.CreateFromTask(OpenFile, this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));
        LocateCommand =
            ReactiveCommand.CreateFromTask(LocateFile, this.WhenAnyValue(x => x.SelectedImage).Select(x => x != null));
        ClearTagFilterInputCommand = ReactiveCommand.Create(() => { TagFilterInput = string.Empty; },
            this.WhenAnyValue(x => x.TagFilterInput).Select(x => !string.IsNullOrWhiteSpace(x)));

    }
    private async Task OpenFile()
    {
        if (SelectedImage == null || WorkingFolder == null)
        {
            return;
        }

        string path = Path.Combine(WorkingFolder, SelectedImage.Item);

        if (File.Exists(path))
        {
            await new FileManagerHandler().OpenFile(path);
        }
    }


    private async Task LocateFile()
    {
        if (SelectedImage == null || WorkingFolder == null)
        {
            return;
        }

        string path = Path.Combine(WorkingFolder, SelectedImage.Item);

        if (File.Exists(path))
        {
            await new FileManagerHandler().RevealFileInFolder(path);
        }
    }
}
using Avalonia.ReactiveUI;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Views;

public partial class TagToImagesView : ReactiveUserControl<TagToImagesViewModel>
{
    public TagToImagesView()
    {
        InitializeComponent();
    }
}
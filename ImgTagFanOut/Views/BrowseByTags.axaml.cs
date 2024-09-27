using Avalonia.ReactiveUI;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Views;

public partial class BrowseByTags : ReactiveUserControl<ViewByTagViewModel>
{
    public BrowseByTags()
    {
        InitializeComponent();
    }
}
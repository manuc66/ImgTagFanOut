using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Views;

public partial class AssignTags : ReactiveUserControl<MainWindowViewModel>
{
    public AssignTags()
    {
        InitializeComponent();
    }
}
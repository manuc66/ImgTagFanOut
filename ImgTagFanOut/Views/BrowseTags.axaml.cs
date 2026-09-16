using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Views;

public partial class BrowseTags : UserControl
{
    public BrowseTags()
    {
        InitializeComponent();
        TagFilterTextBox.KeyDown += OnTagFilterKeyDown;
    }

    private void OnTagFilterKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel vm)
        {
            ICommand cmd = vm.AddToTagListCommand;
            if (cmd.CanExecute(null))
            {
                cmd.Execute(null);
            }
        }
    }
}
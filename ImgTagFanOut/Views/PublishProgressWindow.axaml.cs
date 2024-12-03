using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ImgTagFanOut.ViewModels;
using ReactiveUI;

namespace ImgTagFanOut.Views;

public partial class PublishProgressWindow : ReactiveWindow<PublishProgressViewModel>
{
    public PublishProgressWindow()
    {
        InitializeComponent();

        this.WhenActivated(d => d(ViewModel!.CloseCommand.Subscribe(x => Close())));

        TextBox? searchLog = this.FindControl<TextBox>("SearchLog");
        searchLog!.PropertyChanged += (_, _) =>
        {
            // https://github.com/AvaloniaUI/Avalonia/issues/3036
            int? lastIndexOf = searchLog.Text?.LastIndexOf(Environment.NewLine);
            searchLog.CaretIndex = lastIndexOf.HasValue ? lastIndexOf.Value + 1 : 0;
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            //https://github.com/AvaloniaUI/Avalonia/issues/6671
            searchLog.FontFamily = new("Courier New");
        }
    }

    private void Window_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (ViewModel?.Completed != true)
        {
            e.Cancel = true;
        }
    }
}

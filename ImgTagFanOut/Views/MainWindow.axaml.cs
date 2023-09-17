using System;
using System.Threading.Tasks;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using ImgTagFanOut.ViewModels;
using ReactiveUI;

namespace ImgTagFanOut.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
        this.WhenActivated(d => d(ViewModel!.ShowPublishProgressDialog.RegisterHandler(DoShowPublishProgressDialogAsync)));
        this.WhenActivated(d => d(ViewModel!.ShowConsentDialog.RegisterHandler(DoShowConsentDialogAsync)));
        Activated += OnWindowActivated;
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() => ViewModel!.WindowActivated = true);
    }

    private async Task DoShowPublishProgressDialogAsync(InteractionContext<PublishProgressViewModel, int?> interaction)
    {
        PublishProgressWindow dialog = new()
        {
            DataContext = interaction.Input
        };

        int? result = await dialog.ShowDialog<int?>(this);
        interaction.SetOutput(result);
    }
    private async Task DoShowConsentDialogAsync(InteractionContext<ConsentViewModel, int?> interaction)
    {
        ConsentWindow dialog = new()
        {
            DataContext = interaction.Input
        };

        int? result = await dialog.ShowDialog<int?>(this);
        interaction.SetOutput(result);
    }
}
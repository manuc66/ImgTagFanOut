using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
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
        this.WhenActivated(d => d(ViewModel!.PublishDropOrMergeDialog.RegisterHandler(DoShowPublishDropOrMergeDialogAsync)));
        this.WhenActivated(d => d(ViewModel!.ShowPublishProgressDialog.RegisterHandler(DoShowPublishProgressDialogAsync)));
        this.WhenActivated(d => d(ViewModel!.ShowConsentDialog.RegisterHandler(DoShowConsentDialogAsync)));
        this.WhenActivated(d => d(ViewModel!.ShowAboutDialog.RegisterHandler(DoShowABoutDialogAsync)));
        this.WhenActivated(d => d(ViewModel!.ExitCommand.Subscribe(_ => Close())));
        Activated += OnWindowActivated;
        this.WhenActivated(disposable =>
        {
            this.ViewModel.WhenAnyValue(x => x.IsBusy).Do(UpdateCursor).Subscribe().DisposeWith(disposable);
        });
    }

    private void UpdateCursor(bool isBusy)
    {
        if (isBusy)
        {
            Cursor = new Cursor(StandardCursorType.Wait);
        }
        else
        {
            Cursor = Cursor.Default;
        }
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() => ViewModel!.WindowActivated = true);
    }

    private async Task DoShowPublishDropOrMergeDialogAsync(IInteractionContext<PublishDropOrMergeViewModel, int?> interaction)
    {
        PublishDropOrMergeWindow dialog = new() { DataContext = interaction.Input };

        int? result = await dialog.ShowDialog<int?>(this);
        interaction.SetOutput(result);
    }

    private async Task DoShowPublishProgressDialogAsync(IInteractionContext<PublishProgressViewModel, int?> interaction)
    {
        PublishProgressWindow dialog = new() { DataContext = interaction.Input };

        int? result = await dialog.ShowDialog<int?>(this);
        interaction.SetOutput(result);
    }

    private async Task DoShowConsentDialogAsync(IInteractionContext<ConsentViewModel, int?> interaction)
    {
        ConsentWindow dialog = new() { DataContext = interaction.Input };

        int? result = await dialog.ShowDialog<int?>(this);
        interaction.SetOutput(result);
    }

    private async Task DoShowABoutDialogAsync(IInteractionContext<AboutViewModel, int?> interaction)
    {
        About dialog = new() { DataContext = interaction.Input };

        int? result = await dialog.ShowDialog<int?>(this);
        interaction.SetOutput(result);
    }
}

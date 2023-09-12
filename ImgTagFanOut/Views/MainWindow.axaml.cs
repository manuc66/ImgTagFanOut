using System.Threading.Tasks;
using Avalonia.ReactiveUI;
using ImgTagFanOut.ViewModels;
using ReactiveUI;

namespace ImgTagFanOut.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
        this.WhenActivated(d => d(ViewModel!.ShowDialog.RegisterHandler(DoShowDialogAsync)));
    }

    private async Task DoShowDialogAsync(InteractionContext<PublishProgressViewModel, int?> interaction)
    {
        PublishProgressWindow dialog = new PublishProgressWindow();
        dialog.DataContext = interaction.Input;

        int? result = await dialog.ShowDialog<int?>(this);
        interaction.SetOutput(result);
    }
}
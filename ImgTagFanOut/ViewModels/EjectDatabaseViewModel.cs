using System.Reactive;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

public class EjectDatabaseViewModel : ViewModelBase
{
    private bool? _confirmed;

    public EjectDatabaseViewModel()
        : this(string.Empty) { }

    public EjectDatabaseViewModel(string folderPath)
    {
        FolderPath = folderPath;
        Message = string.Format(global::ImgTagFanOut.Resources.Resources.EjectDatabaseConfirmation, folderPath);

        EjectCommand = ReactiveCommand.Create(() => { Confirmed = true; });
        CancelCommand = ReactiveCommand.Create(() => { Confirmed = false; });
    }

    public string FolderPath { get; }

    public string Message { get; }

    public bool? Confirmed
    {
        get => _confirmed;
        private set => this.RaiseAndSetIfChanged(ref _confirmed, value);
    }

    public ReactiveCommand<Unit, Unit> EjectCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
}

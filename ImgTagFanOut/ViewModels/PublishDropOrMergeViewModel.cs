using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

public class PublishDropOrMergeViewModel : ViewModelBase
{
    private bool _replaceIsConfirmed = false;
    private bool? _merge;
    public ReactiveCommand<Unit, Unit> MergeCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ReplaceCommand { get; set;  }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public PublishDropOrMergeViewModel()
    {
        CancelCommand = ReactiveCommand.Create(() =>
        {
            Merge = null;
        });
        MergeCommand = ReactiveCommand.Create(() =>
        {
            Merge = true;
        });
        ReplaceCommand = ReactiveCommand.Create(() =>
        {
            if (ReplaceIsConfirmed)
            {
                Merge = false;
            }
        }, this.WhenAnyValue(x => x.ReplaceIsConfirmed));
    }

    public bool? Merge  {
        get => _merge;
        set => this.RaiseAndSetIfChanged(ref _merge, value);
    }
    public bool ReplaceIsConfirmed     {
        get => _replaceIsConfirmed;
        set => this.RaiseAndSetIfChanged(ref _replaceIsConfirmed, value);
    }
}

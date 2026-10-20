using System;
using Avalonia.ReactiveUI;
using ImgTagFanOut.ViewModels;
using ReactiveUI;

namespace ImgTagFanOut.Views;

public partial class EjectDatabaseWindow : ReactiveWindow<EjectDatabaseViewModel>
{
    public EjectDatabaseWindow()
    {
        InitializeComponent();

        this.WhenActivated(d => d(ViewModel!.EjectCommand.Subscribe(_ => Close())));
        this.WhenActivated(d => d(ViewModel!.CancelCommand.Subscribe(_ => Close())));
    }
}

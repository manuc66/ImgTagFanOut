using System;
using Avalonia.ReactiveUI;
using ImgTagFanOut.ViewModels;
using ReactiveUI;

namespace ImgTagFanOut.Views;

public partial class ConsentWindow : ReactiveWindow<ConsentViewModel>
{
    public ConsentWindow()
    {
        InitializeComponent();

        this.WhenActivated(d => d(ViewModel!.AcceptCommand.Subscribe(x => Close())));
        this.WhenActivated(d => d(ViewModel!.DeclineCommand.Subscribe(x => Close())));
    }
}

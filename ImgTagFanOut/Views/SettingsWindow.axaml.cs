using System;
using Avalonia.ReactiveUI;
using ImgTagFanOut.ViewModels;
using ReactiveUI;

namespace ImgTagFanOut.Views;

public partial class SettingsWindow : ReactiveWindow<SettingsViewModel>
{
    public SettingsWindow()
    {
        InitializeComponent();

        this.WhenActivated(d => d(ViewModel!.CancelCommand.Subscribe(_ => { Close(); })));
        this.WhenActivated(d => d(ViewModel!.SaveCommand.Subscribe(_ => { Close(); })));
    }
}

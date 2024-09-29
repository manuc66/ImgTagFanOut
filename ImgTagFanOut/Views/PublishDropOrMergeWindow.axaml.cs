using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ImgTagFanOut.ViewModels;
using ReactiveUI;

namespace ImgTagFanOut.Views;

public partial class PublishDropOrMergeWindow  : ReactiveWindow<PublishDropOrMergeViewModel>
{
    public PublishDropOrMergeWindow()
    {
        InitializeComponent();
        
        this.WhenActivated(d => d(ViewModel!.CancelCommand.Subscribe(x =>
        {
            Close();
        }))); 
        this.WhenActivated(d => d(ViewModel!.MergeCommand.Subscribe(x =>
        {
            Close();
        }))); 
        this.WhenActivated(d => d(ViewModel!.ReplaceCommand.Subscribe(x =>
        {
            if (ViewModel!.ReplaceIsConfirmed)
            {
                Close();
            }
        })));
    }
}


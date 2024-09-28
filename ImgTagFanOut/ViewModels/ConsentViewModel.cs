using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using ImgTagFanOut.Models;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

public class ConsentViewModel : ViewModelBase
{
    private bool _consentErrorTracking;

    public bool ConsentErrorTracking
    {
        get => _consentErrorTracking;
        set => this.RaiseAndSetIfChanged(ref _consentErrorTracking, value);
    }

    public ReactiveCommand<Window, Unit> AcceptCommand { get; }
    public ReactiveCommand<Window, Unit> DeclineCommand { get; }

    public ConsentViewModel()
    {
        ConsentErrorTracking = true;
        Settings settings = new();
        AcceptCommand = ReactiveCommand.CreateFromTask((Window _) => Task.CompletedTask, Observable.Return(true));
        AcceptCommand.Subscribe(_ =>
        {
            AppSettings appSettings = settings.ReadSettings();
            appSettings.ErrorTrackingAllowed = ConsentErrorTracking;
            settings.Save(appSettings);
            if (!ConsentErrorTracking)
            {
                Program.ErrorTracking?.Dispose();
            }
        });
        DeclineCommand = ReactiveCommand.CreateFromTask((Window _) => Task.CompletedTask, Observable.Return(true));
        DeclineCommand.Subscribe(_ =>
        {
            AppSettings appSettings = settings.ReadSettings();
            appSettings.ErrorTrackingAllowed = false;
            settings.Save(appSettings);
            Program.ErrorTracking?.Dispose();
        });
    }
}

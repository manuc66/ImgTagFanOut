using System.Reactive;
using ImgTagFanOut.Models;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

/// <summary>
/// Backs the advanced settings window. Values are read from the user settings
/// file when the window opens and written back by the save command; cancelling
/// writes nothing.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly Settings _settings;
    private bool _useHardLinks;

    public SettingsViewModel()
        : this(new Settings()) { }

    internal SettingsViewModel(Settings settings)
    {
        _settings = settings;
        _useHardLinks = settings.ReadSettings().LinkMode == PublishLinkMode.HardLinkWhenPossible;
        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(() => { Confirmed = false; });
    }

    /// <summary>True when the window closes by saving; false on cancel.</summary>
    public bool Confirmed { get; private set; }

    public bool UseHardLinks
    {
        get => _useHardLinks;
        set => this.RaiseAndSetIfChanged(ref _useHardLinks, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    private void Save()
    {
        AppSettings appSettings = _settings.ReadSettings();
        appSettings.LinkMode = _useHardLinks ? PublishLinkMode.HardLinkWhenPossible : PublishLinkMode.Copy;
        _settings.Save(appSettings);
        Confirmed = true;
    }
}

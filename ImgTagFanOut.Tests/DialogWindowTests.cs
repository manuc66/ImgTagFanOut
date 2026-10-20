using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ImgTagFanOut.Models;
using ImgTagFanOut.ViewModels;
using ImgTagFanOut.Views;

namespace ImgTagFanOut.Tests;

/// <summary>
/// Covers the dialog windows and the <c>WhenActivated</c> wiring that binds them to their
/// view models. That wiring is what Avalonia.ReactiveUI provides: subscriptions are attached
/// when a control joins the visual tree and torn down when it leaves, so the commands and
/// <see cref="ReactiveUI.Interaction{TInput,TOutput}"/> handlers registered inside
/// <c>WhenActivated</c> only exist while the window is open.
/// </summary>
public class DialogWindowTests : IDisposable
{
    private readonly string _tempDir;

    public DialogWindowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ImgTagFanOut.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    // ---------------------------------------------------------------- PublishDropOrMerge

    [AvaloniaFact]
    public void PublishDropOrMerge_CancelButton_ClosesWindowAndClearsChoice()
    {
        PublishDropOrMergeViewModel vm = new() { Merge = true };
        PublishDropOrMergeWindow window = new() { DataContext = vm };
        Func<bool> isOpen = window.TrackOpen();

        Button cancel = window.FindControl<Button>("CancelButton")!;
        Assert.NotNull(cancel);
        Execute(cancel.Command);

        Assert.False(isOpen());
        Assert.Null(vm.Merge);
    }

    [AvaloniaFact]
    public void PublishDropOrMerge_AddNewButton_ClosesWindowAndKeepsMerge()
    {
        PublishDropOrMergeViewModel vm = new();
        PublishDropOrMergeWindow window = new() { DataContext = vm };
        Func<bool> isOpen = window.TrackOpen();

        Button addNew = window.FindControl<Button>("AddNewButton")!;
        Assert.NotNull(addNew);
        Execute(addNew.Command);

        Assert.False(isOpen());
        Assert.True(vm.Merge);
    }

    [AvaloniaFact]
    public void PublishDropOrMerge_ReplaceButton_StaysOpenUntilConfirmed()
    {
        PublishDropOrMergeViewModel vm = new();
        PublishDropOrMergeWindow window = new() { DataContext = vm };
        Func<bool> isOpen = window.TrackOpen();

        Button replace = window.FindControl<Button>("ReplaceButton")!;
        Assert.NotNull(replace);

        // ReplaceCommand is gated on ReplaceIsConfirmed, so the button cannot fire yet.
        Assert.False(replace.IsEffectivelyEnabled);

        // Confirm, then the same button must both record the choice and close the window.
        vm.ReplaceIsConfirmed = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(replace.IsEffectivelyEnabled);

        Execute(replace.Command);

        Assert.False(isOpen());
        Assert.False(vm.Merge);
    }

    // ----------------------------------------------------------------- PublishProgress

    [AvaloniaFact]
    public void PublishProgress_CloseIsRefusedWhilePublishHasNotCompleted()
    {
        FakePublisher publisher = new();
        PublishProgressViewModel vm = new(_tempDir, _tempDir, false, PublishLinkMode.Copy, CancellationToken.None, publisher, new FakeFileManagerHandler());
        PublishProgressWindow window = new() { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(publisher.WasCalled);
        Assert.False(vm.Completed);

        window.Close();
        Dispatcher.UIThread.RunJobs();

        // Window_OnClosing cancels the close while Completed is false.
        Assert.True(window.IsVisible);
    }

    [AvaloniaFact]
    public async Task PublishProgress_CloseSucceedsOnceCompleted()
    {
        FakePublisher publisher = new();
        PublishProgressViewModel vm = new(_tempDir, _tempDir, false, PublishLinkMode.Copy, CancellationToken.None, publisher, new FakeFileManagerHandler());
        PublishProgressWindow window = new() { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.False(vm.Completed);

        // CloseCommand is gated on Completed, so it must be inert until the publish finished.
        Button close = window.FindButtonForCommand(vm.CloseCommand)!;
        Assert.NotNull(close);
        Assert.False(close.IsEffectivelyEnabled);

        publisher.Complete();
        await SettleAsync();
        Assert.True(vm.Completed);
        Assert.True(close.IsEffectivelyEnabled);

        Func<bool> isOpen = window.TrackOpen();
        await ExecuteAsync(close.Command);
        Assert.False(isOpen());
    }

    [AvaloniaFact]
    public async Task PublishProgress_SearchLog_CaretFollowsTheLastWrittenLine()
    {
        FakePublisher publisher = new();
        publisher.OnStart = (beginTag, onFileCompleted, _, _) =>
            onFileCompleted(new PublishFileResult("/src/a.png", "/dst/a.png", true, false));

        PublishProgressViewModel vm = new(_tempDir, _tempDir, false, PublishLinkMode.Copy, CancellationToken.None, publisher, new FakeFileManagerHandler());
        PublishProgressWindow window = new() { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        TextBox searchLog = window.FindControl<TextBox>("SearchLog")!;
        Assert.NotNull(searchLog);

        vm.TrailLog += "first line";
        Dispatcher.UIThread.RunJobs();
        vm.TrailLog += $"{Environment.NewLine}second line";
        Dispatcher.UIThread.RunJobs();

        // Workaround for https://github.com/AvaloniaUI/Avalonia/issues/3036: the caret is
        // parked right after the last newline so the newest line stays visible.
        int lastNewLine = searchLog.Text!.LastIndexOf(Environment.NewLine, StringComparison.Ordinal);
        Assert.True(lastNewLine >= 0);
        Assert.Equal(lastNewLine + 1, searchLog.CaretIndex);

        publisher.Complete();
        await SettleAsync();
    }

    [AvaloniaFact]
    public async Task PublishProgress_OpenButton_OpensTheTargetFolder()
    {
        string target = Path.Combine(_tempDir, "target");
        Directory.CreateDirectory(target);

        FakePublisher publisher = new();
        FakeFileManagerHandler fileManager = new();
        PublishProgressViewModel vm = new(_tempDir, target, false, PublishLinkMode.Copy, CancellationToken.None, publisher, fileManager);
        PublishProgressWindow window = new() { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Button open = window.FindButtonForCommand(vm.OpenTargetFolderCommand)!;
        Assert.NotNull(open);
        await ExecuteAsync(open.Command);

        Assert.Equal(target, fileManager.LastOpenedFolder);

        publisher.Complete();
        await SettleAsync();
    }

    // -------------------------------------------------------------------- Settings

    [AvaloniaFact]
    public void Settings_CancelKeepsTheCurrentMode()
    {
        using IDisposable _ = UserSettings.Preserve();
        UserSettings.Write("""{"LinkMode":"Copy"}""");

        SettingsViewModel vm = new() { UseHardLinks = true };
        SettingsWindow window = new() { DataContext = vm };
        Func<bool> isOpen = window.TrackOpen();

        Execute(window.FindControl<Button>("CancelButton")!.Command);

        Assert.False(isOpen());
        Assert.False(vm.Confirmed);
        Assert.Equal(PublishLinkMode.Copy, new Settings().ReadSettings().LinkMode);
    }

    [AvaloniaFact]
    public void Settings_SaveStoresTheModeAndCloses()
    {
        using IDisposable _ = UserSettings.Preserve();
        UserSettings.Write("{}");

        SettingsViewModel vm = new() { UseHardLinks = true };
        SettingsWindow window = new() { DataContext = vm };
        Func<bool> isOpen = window.TrackOpen();

        Execute(window.FindControl<Button>("SaveButton")!.Command);

        Assert.False(isOpen());
        Assert.True(vm.Confirmed);
        Assert.Equal(PublishLinkMode.HardLinkWhenPossible, new Settings().ReadSettings().LinkMode);
    }

    // ----------------------------------------------------------------------- Consent

    [AvaloniaFact]
    public async Task Consent_DeclineButton_ClosesWindow()
    {
        using IDisposable _ = UserSettings.Preserve();
        UserSettings.Write("{}");

        ConsentViewModel vm = new();
        ConsentWindow window = new() { DataContext = vm };
        Func<bool> isOpen = window.TrackOpen();

        Button decline = window.FindButtonForCommand(vm.DeclineCommand)!;
        Assert.NotNull(decline);
        await ExecuteAsync(decline.Command);

        Assert.False(isOpen());
    }

    [AvaloniaFact]
    public async Task Consent_AcceptButton_ClosesWindowAndStoresTheChoice()
    {
        using IDisposable _ = UserSettings.Preserve();
        UserSettings.Write("{}");

        ConsentViewModel vm = new();
        ConsentWindow window = new() { DataContext = vm };
        Func<bool> isOpen = window.TrackOpen();

        vm.ConsentErrorTracking = false;

        Button accept = window.FindButtonForCommand(vm.AcceptCommand)!;
        Assert.NotNull(accept);
        await ExecuteAsync(accept.Command);

        Assert.False(isOpen());
        Assert.False(new Settings().ReadSettings().ErrorTrackingAllowed);
    }

    // ------------------------------------------------------------------ EjectDatabase

    [AvaloniaFact]
    public void EjectDatabase_CancelButton_ClosesWindowWithoutConfirming()
    {
        EjectDatabaseViewModel vm = new(_tempDir);
        EjectDatabaseWindow window = new() { DataContext = vm };
        Func<bool> isOpen = window.TrackOpen();

        Button cancel = window.FindControl<Button>("CancelButton")!;
        Assert.NotNull(cancel);
        Execute(cancel.Command);

        Assert.False(isOpen());
        Assert.False(vm.Confirmed);
    }

    [AvaloniaFact]
    public void EjectDatabase_EjectButton_ClosesWindowAndConfirms()
    {
        EjectDatabaseViewModel vm = new(_tempDir);
        EjectDatabaseWindow window = new() { DataContext = vm };
        Func<bool> isOpen = window.TrackOpen();

        Button eject = window.FindControl<Button>("EjectButton")!;
        Assert.NotNull(eject);
        Execute(eject.Command);

        Assert.False(isOpen());
        Assert.True(vm.Confirmed);
    }

    // ------------------------------------------------- MainWindow activation regression

    [AvaloniaFact]
    public void MainWindow_ActivationRegistersInteractionHandlers()
    {
        // Force the consent prompt to be due, so that activating the window really does go
        // through ShowConsentDialog.Handle(...).
        using IDisposable _ = UserSettings.Preserve();
        UserSettings.Write("{}");

        MainWindowViewModel vm = new() { WorkingFolder = _tempDir };
        MainWindow window = new() { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // The dialog handlers live inside MainWindow's WhenActivated blocks. If those
        // registrations are missing, ReactiveUI surfaces UnhandledInteractionException here.
        vm.WindowActivated = true;
        Dispatcher.UIThread.RunJobs();

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    // ------------------------------------------------------------------------ Helpers

    private static void Execute(ICommand? command)
    {
        Assert.NotNull(command);
        command.Execute(null);
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task ExecuteAsync(ICommand? command)
    {
        Assert.NotNull(command);
        command.Execute(null);
        await SettleAsync();
    }

    [AvaloniaFact]
    public void About_OpensWithoutDependingOnTheWorkingDirectory()
    {
        // The license ships next to the executable. It used to be read from the current
        // directory, which made the window throw when the app ran from its project folder.
        About about = new();

        TextBlock? license = about.FindControl<TextBlock>("LicenseTextBlock");
        Assert.NotNull(license);
        Assert.False(string.IsNullOrWhiteSpace(license!.Text));
    }

    /// <summary>
    /// Lets queued dispatcher work and the continuations posted by the commands run. Command
    /// execution is asynchronous, so tests must pump before asserting.
    /// </summary>
    private static async Task SettleAsync()
    {
        for (int i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        Dispatcher.UIThread.RunJobs();
    }
}

/// <summary>
/// Helpers around the real per-user <c>settings.json</c>. A few view models read and write it
/// directly (no seam was introduced), so tests that reach them snapshot the file and put it
/// back to avoid mutating the developer's own configuration.
/// </summary>
internal static class UserSettings
{
    private static string Path_ => EnvironmentService.GetAppSettingFile();

    public static IDisposable Preserve()
    {
        string path = Path_;
        byte[]? original = File.Exists(path) ? File.ReadAllBytes(path) : null;
        return new Restore(original, path);
    }

    public static void Write(string json) => File.WriteAllText(Path_, json);

    private sealed class Restore : IDisposable
    {
        private readonly byte[]? _original;
        private readonly string _path;

        public Restore(byte[]? original, string path)
        {
            _original = original;
            _path = path;
        }

        public void Dispose()
        {
            if (_original is null)
            {
                File.Delete(_path);
            }
            else
            {
                File.WriteAllBytes(_path, _original);
            }
        }
    }
}
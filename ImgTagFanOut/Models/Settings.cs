using System;
using System.IO;
using System.Text.Json;
using Serilog;

namespace ImgTagFanOut.Models;

class Settings
{
    private readonly string _appSettingFile;

    public Settings()
    {
        _appSettingFile = EnvironmentService.GetAppSettingFile();
    }

    internal Settings(string appSettingFile)
    {
        _appSettingFile = appSettingFile;
    }

    internal AppSettings ReadSettings()
    {
        try
        {
            if (!File.Exists(_appSettingFile))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(_appSettingFile);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);
            if (settings is null)
            {
                return new AppSettings();
            }

            // A hand-edited or newer file must not leave an undefined mode
            // behind: everything except the explicit hard-link value copies.
            if (!Enum.IsDefined(settings.LinkMode))
            {
                settings.LinkMode = PublishLinkMode.Copy;
            }

            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            // The settings file is a convenience, never a reason not to start.
            Log.Warning(ex, "Cannot read the settings file {File}, using defaults", _appSettingFile);
            return new AppSettings();
        }
    }

    internal void Save(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings);

        string? folder = Path.GetDirectoryName(_appSettingFile);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        // Write next to the target then move it over: a crash mid-write must
        // not leave a truncated settings file that loses the user's choices.
        string temporaryFile = _appSettingFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryFile, json);
            File.Move(temporaryFile, _appSettingFile, overwrite: true);
        }
        catch
        {
            TryDelete(temporaryFile);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Cannot remove the temporary settings file {File}", path);
        }
    }
}

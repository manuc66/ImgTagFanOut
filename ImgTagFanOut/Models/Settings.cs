using System.IO;
using System.Text.Json;

namespace ImgTagFanOut.Models;

class Settings
{
    private readonly string _appSettingFile;

    public Settings()
    {
        _appSettingFile = EnvironmentService.GetAppSettingFile();
    }

    internal AppSettings ReadSettings()
    {
        AppSettings appSettings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_appSettingFile)) ?? new AppSettings();
        return appSettings;
    }

    internal void Save(AppSettings settings)
    {
        File.WriteAllText(_appSettingFile, JsonSerializer.Serialize(settings));
    }
}
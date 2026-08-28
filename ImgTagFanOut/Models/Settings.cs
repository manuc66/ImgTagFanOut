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
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    internal void Save(AppSettings settings)
    {
        File.WriteAllText(_appSettingFile, JsonSerializer.Serialize(settings));
    }
}

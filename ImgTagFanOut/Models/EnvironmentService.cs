using System;
using System.IO;

namespace ImgTagFanOut.Models;

public static class EnvironmentService
{
    private static string GetSettingsFolder()
    {
        // Retrieve the settings folder using the Avalonia.Application class.
        string appSettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(ImgTagFanOut));

        // Create the folder if it doesn't exist.
        Directory.CreateDirectory(appSettingsFolder);

        return appSettingsFolder;
    }

    internal static string GetAppSettingFile()
    {
        string appSettingFile = Path.Combine(GetSettingsFolder(), "settings.json");

        if (!File.Exists(appSettingFile))
        {
            File.WriteAllText(appSettingFile, "{}");
        }

        return appSettingFile;
    }
    internal static string GetLogFile()
    {
        string appSettingFile = Path.Combine(GetSettingsFolder(), "log.txt");

        if (!File.Exists(appSettingFile))
        {
            File.WriteAllText(appSettingFile, "{}");
        }

        return appSettingFile;
    }

    internal static string GetMyPictureFolder()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    }
}
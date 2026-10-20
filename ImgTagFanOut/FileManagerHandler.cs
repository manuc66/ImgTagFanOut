using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Serilog;

namespace ImgTagFanOut;

public class FileManagerHandler : IFileManagerHandler
{
    public async Task OpenParentFolder(string path)
    {
        string? parentFolder = Path.GetDirectoryName(path);

        if (parentFolder is null)
        {
            return;
        }

        await OpenFolder(parentFolder);
    }

    public Task OpenFolder(string parentFolder)
    {
        return StartProcessAsync(new ProcessStartInfo
        {
            FileName = parentFolder,
            UseShellExecute = true,
        });
    }

    public Task OpenFile(string path)
    {
        return StartProcessAsync(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
    }

    private static async Task StartProcessAsync(ProcessStartInfo startInfo)
    {
        try
        {
            using Process process = new() { StartInfo = startInfo };
            if (process.Start())
            {
                await process.WaitForExitAsync();
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException or FileNotFoundException)
        {
            // No file manager, no handler for this extension, a headless
            // session: opening a folder is a convenience, not a requirement.
            Log.Warning(ex, "Cannot open {Target}", startInfo.FileName);
        }
    }

    internal static ProcessStartInfo? BuildRevealProcessStartInfo(string path, OSPlatform platform)
    {
        if (platform == OSPlatform.Windows)
        {
            return new()
            {
                FileName = "explorer",
                // The path must be quoted as a whole: without the opening quote,
                // explorer receives "/select,C:\...\file.jpg\"".
                Arguments = "/select,\"" + path + "\"",
                UseShellExecute = true,
            };
        }

        if (platform == OSPlatform.OSX)
        {
            return new()
            {
                FileName = "open",
                Arguments = "-R \"" + path + "\"",
                UseShellExecute = true,
            };
        }

        if (platform == OSPlatform.Linux)
        {
            // On linux, try to use dbus, see https://stackoverflow.com/questions/73409227/open-file-in-containing-folder-for-linux/73409251
            string uri = new Uri(Path.GetFullPath(path)).AbsoluteUri;
            return new()
            {
                FileName = "dbus-send",
                Arguments =
                    $@"--print-reply --dest=org.freedesktop.FileManager1 --type=method_call /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems array:string:""{uri}"" string:""""",
                UseShellExecute = true,
            };
        }

        return null;
    }

    public async Task RevealFileInFolder(string path)
    {
        OSPlatform platform = GetCurrentPlatform();
        ProcessStartInfo? startInfo = BuildRevealProcessStartInfo(path, platform);

        if (startInfo == null)
        {
            await OpenParentFolder(path);
            return;
        }

        try
        {
            using Process process = new() { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync();

            if (platform == OSPlatform.Linux && process.ExitCode != 0)
            {
                // The dbus invocation can fail for a variety of reasons:
                // - dbus is not available
                // - no programs implement the service,
                // - ...
                await OpenParentFolder(path);
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            Log.Warning(ex, "Cannot reveal {File}, opening its folder instead", path);
            await OpenParentFolder(path);
        }
    }

    private static OSPlatform GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return OSPlatform.Windows;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return OSPlatform.OSX;

        return OSPlatform.Linux;
    }
}

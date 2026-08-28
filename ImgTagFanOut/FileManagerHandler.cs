using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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

    public async Task OpenFolder(string parentFolder)
    {
        using Process folderOpener = new();
        folderOpener.StartInfo.FileName = parentFolder;
        folderOpener.StartInfo.UseShellExecute = true;

        if (folderOpener.Start())
        {
            await folderOpener.WaitForExitAsync();
        }
    }

    public async Task OpenFile(string path)
    {
        // Just open the directory instead
        using Process folderOpener = new();
        folderOpener.StartInfo.FileName = path;
        folderOpener.StartInfo.UseShellExecute = true;

        if (folderOpener.Start())
        {
            await folderOpener.WaitForExitAsync();
        }
    }

    internal static ProcessStartInfo? BuildRevealProcessStartInfo(string path, OSPlatform platform)
    {
        if (platform == OSPlatform.Windows)
        {
            return new()
            {
                FileName = "explorer",
                Arguments = "/select," + path + "\"",
                UseShellExecute = true,
            };
        }

        if (platform == OSPlatform.OSX)
        {
            return new()
            {
                FileName = "open",
                Arguments = "-R " + path,
                UseShellExecute = true,
            };
        }

        if (platform == OSPlatform.Linux)
        {
            // On linux, try to use dbus, see https://stackoverflow.com/questions/73409227/open-file-in-containing-folder-for-linux/73409251
            return new()
            {
                FileName = "dbus-send",
                Arguments =
                    $@"--print-reply --dest=org.freedesktop.FileManager1 --type=method_call /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems array:string:""file://{path}"" string:""""",
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

    private static OSPlatform GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return OSPlatform.Windows;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return OSPlatform.OSX;

        return OSPlatform.Linux;
    }
}

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ImgTagFanOut;

public class FileManagerHandler
{
    public async Task OpenParentFolder(string path)
    {
        string? parentFolder = Path.GetDirectoryName(path);
        if (parentFolder is null) return;
        await OpenFolder(parentFolder);
    }

    public async Task OpenFolder(string parentFolder)
    {
        using Process folderOpener = new();
        folderOpener.StartInfo.FileName = parentFolder;
        folderOpener.StartInfo.UseShellExecute = true;
        folderOpener.Start();
        await folderOpener.WaitForExitAsync();
    }

    public async Task OpenFile(string path)
    {
        // Just open the directory instead
        using Process folderOpener = new();
        folderOpener.StartInfo.FileName = path;
        folderOpener.StartInfo.UseShellExecute = true;
        folderOpener.Start();
        await folderOpener.WaitForExitAsync();
    }
    public async Task RevealFileInFolder(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using Process fileOpener = new();
            fileOpener.StartInfo.FileName = "explorer";
            fileOpener.StartInfo.Arguments = "/select," + path + "\"";
            fileOpener.Start();
            await fileOpener.WaitForExitAsync();
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            using Process fileOpener = new();
            fileOpener.StartInfo.FileName = "explorer";
            fileOpener.StartInfo.Arguments = "-R " + path;
            fileOpener.Start();
            await fileOpener.WaitForExitAsync();
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // On linux, try to use dbus, see https://stackoverflow.com/questions/73409227/open-file-in-containing-folder-for-linux/73409251
            Process? dbusShowItemsProcess = null;
            try
            {
                dbusShowItemsProcess = new()
                {
                    StartInfo = new()
                    {
                        FileName = "dbus-send",
                        Arguments =
                            $@"--print-reply --dest=org.freedesktop.FileManager1 --type=method_call /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems array:string:""file://{path}"" string:""""",
                        UseShellExecute = true
                    }
                };
                dbusShowItemsProcess.Start();
                await dbusShowItemsProcess.WaitForExitAsync();

                if (dbusShowItemsProcess.ExitCode == 0)
                {
                    // The dbus invocation can fail for a variety of reasons:
                    // - dbus is not available
                    // - no programs implement the service,
                    // - ...
                    return;
                }
            }
            finally
            {
                dbusShowItemsProcess?.Dispose();
            }
        }

        await OpenParentFolder(path);
    }
}

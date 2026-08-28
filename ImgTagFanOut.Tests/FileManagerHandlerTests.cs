using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ImgTagFanOut.Tests;

public class FileManagerHandlerTests
{
    [Fact]
    public void BuildRevealProcessStartInfo_OnMacOs_UsesOpenWithRevealFlag()
    {
        ProcessStartInfo? info = FileManagerHandler.BuildRevealProcessStartInfo("/tmp/some file.jpg", OSPlatform.OSX);

        Assert.NotNull(info);
        Assert.Equal("open", info.FileName);
        Assert.Contains("-R", info.Arguments);
        Assert.Contains("/tmp/some file.jpg", info.Arguments);
    }

    [Fact]
    public void BuildRevealProcessStartInfo_OnWindows_UsesExplorerWithSelectFlag()
    {
        ProcessStartInfo? info = FileManagerHandler.BuildRevealProcessStartInfo(@"C:\tmp\some file.jpg", OSPlatform.Windows);

        Assert.NotNull(info);
        Assert.Equal("explorer", info.FileName);
        Assert.Contains("/select,", info.Arguments);
    }

    [Fact]
    public void BuildRevealProcessStartInfo_OnLinux_UsesDbusSend()
    {
        ProcessStartInfo? info = FileManagerHandler.BuildRevealProcessStartInfo("/tmp/some file.jpg", OSPlatform.Linux);

        Assert.NotNull(info);
        Assert.Equal("dbus-send", info.FileName);
        Assert.Contains("org.freedesktop.FileManager1.ShowItems", info.Arguments);
        Assert.Contains("file:///tmp/some file.jpg", info.Arguments);
    }
}
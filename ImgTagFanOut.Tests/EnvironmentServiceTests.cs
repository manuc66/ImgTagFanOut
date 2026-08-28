using ImgTagFanOut.Models;

namespace ImgTagFanOut.Tests;

public class EnvironmentServiceTests : IDisposable
{
    private readonly string _tempDir;

    public EnvironmentServiceTests()
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

    [Fact]
    public void GetLogFile_WhenMissing_ReturnsPathWithoutCreatingSeededContent()
    {
        string logFile = EnvironmentService.GetLogFile(_tempDir);

        Assert.Equal(Path.Combine(_tempDir, "log.txt"), logFile);
        Assert.False(File.Exists(logFile));
    }

    [Fact]
    public void GetLogFile_WhenExists_ReturnsExistingPath()
    {
        string existing = Path.Combine(_tempDir, "log.txt");
        File.WriteAllText(existing, "real log content");

        string logFile = EnvironmentService.GetLogFile(_tempDir);

        Assert.Equal(existing, logFile);
        Assert.Equal("real log content", File.ReadAllText(existing));
    }
}
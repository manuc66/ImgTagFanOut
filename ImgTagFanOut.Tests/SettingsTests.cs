using ImgTagFanOut.Models;

namespace ImgTagFanOut.Tests;

public class SettingsTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsTests()
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

    private string WriteSettingsFile(string content)
    {
        string path = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ReadSettings_WithCorruptFile_ReturnsDefaultsInsteadOfThrowing()
    {
        string path = WriteSettingsFile("{ not valid json !");

        AppSettings settings = new Settings(path).ReadSettings();

        Assert.NotNull(settings);
        Assert.Null(settings.LastFolder);
    }

    [Fact]
    public void ReadSettings_WithMissingFile_ReturnsDefaultsInsteadOfThrowing()
    {
        string path = Path.Combine(_tempDir, "does-not-exist.json");

        AppSettings settings = new Settings(path).ReadSettings();

        Assert.NotNull(settings);
        Assert.Null(settings.LastFolder);
    }

    [Fact]
    public void ReadSettings_WithValidFile_ReadsValues()
    {
        string path = WriteSettingsFile("""{"LastFolder": "/some/folder"}""");

        AppSettings settings = new Settings(path).ReadSettings();

        Assert.Equal("/some/folder", settings.LastFolder);
    }

    [Fact]
    public void SaveThenRead_RoundTrips()
    {
        string path = Path.Combine(_tempDir, "settings.json");
        Settings settings = new(path);
        AppSettings toSave = new() { LastFolder = "/round/trip", ErrorTrackingAllowed = false };

        settings.Save(toSave);
        AppSettings read = new Settings(path).ReadSettings();

        Assert.Equal("/round/trip", read.LastFolder);
        Assert.False(read.ErrorTrackingAllowed);
    }

    [Fact]
    public void LinkMode_DefaultsToCopy_AndRoundTripsAsAString()
    {
        string path = Path.Combine(_tempDir, "settings.json");

        Assert.Equal(PublishLinkMode.Copy, new Settings(path).ReadSettings().LinkMode);

        new Settings(path).Save(new AppSettings { LinkMode = PublishLinkMode.HardLinkWhenPossible });

        // Readable in the JSON file rather than a bare number, so a user can
        // inspect it and an older build ignores it instead of guessing.
        Assert.Contains("HardLinkWhenPossible", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Equal(PublishLinkMode.HardLinkWhenPossible, new Settings(path).ReadSettings().LinkMode);
    }
}
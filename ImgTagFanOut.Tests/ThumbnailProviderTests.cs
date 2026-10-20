using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using ImgTagFanOut.Models;

namespace ImgTagFanOut.Tests;

public class ThumbnailProviderTests : IDisposable
{
    private readonly string _tempDir;

    public ThumbnailProviderTests()
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

    [AvaloniaFact]
    public async Task GetThumbnail_DecodesTheImageAtTheRequestedWidth()
    {
        string path = FakeImageGenerator.WritePng(_tempDir, "wide.png", width: 800, height: 400);

        using Bitmap? thumbnail = await new ThumbnailProvider().GetThumbnail(path, 100);

        Assert.NotNull(thumbnail);
        Assert.Equal(100, thumbnail.PixelSize.Width);
    }

    [AvaloniaFact]
    public async Task GetThumbnail_WithAMissingFile_ReturnsNullInsteadOfThrowing()
    {
        Bitmap? thumbnail = await new ThumbnailProvider().GetThumbnail(Path.Combine(_tempDir, "missing.png"));

        Assert.Null(thumbnail);
    }
}

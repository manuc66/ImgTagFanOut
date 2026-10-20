using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Tests;

public class PublisherTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _workingDir;
    private readonly string _targetDir;

    public PublisherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ImgTagFanOut.Tests", Guid.NewGuid().ToString("N"));
        _workingDir = Path.Combine(_tempDir, "library");
        _targetDir = Path.Combine(_tempDir, "export");
        Directory.CreateDirectory(_workingDir);
        DbContextFactory.ClearTagCache();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private string SourceFile => Path.Combine(_workingDir, "photo.jpg");

    private string ExportedFile => Path.Combine(_targetDir, "Maman", "photo.jpg");

    /// <summary>
    /// Registers one file in the library under one tag, then publishes it. The
    /// library and the export folder share the temp volume, so hard links are
    /// possible unless the filesystem refuses them.
    /// </summary>
    private async Task<PublishFileResult> PublishOnePhotoAsync(PublishLinkMode linkMode, bool createSourceFile = true)
    {
        if (createSourceFile)
        {
            File.WriteAllText(SourceFile, "photo-content");
        }

        await using (IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(_workingDir))
        {
            unitOfWork.TagRepository.TryCreateTag("Maman", out _);
            CanHaveTag item = new("photo.jpg");
            await unitOfWork.TagRepository.AddOrUpdateItem(item, _ => Task.FromResult("hash"));
            await unitOfWork.SaveChangesAsync();

            Tag tag = unitOfWork.TagRepository.GetAllTag().First(t => t.Name == "Maman");
            unitOfWork.TagRepository.AddTagToItem(tag, item);
            await unitOfWork.SaveChangesAsync();
        }

        List<PublishFileResult> results = new();
        await new Publisher().PublishToFolder(
            _workingDir,
            _targetDir,
            false,
            linkMode,
            _ => { },
            results.Add,
            _ => { },
            _ => { },
            CancellationToken.None);

        return Assert.Single(results);
    }

    [Fact]
    public async Task CopyMode_WritesAnIndependentFile()
    {
        PublishFileResult result = await PublishOnePhotoAsync(PublishLinkMode.Copy);

        Assert.True(result.Created);
        Assert.False(result.Linked);
        Assert.True(File.Exists(ExportedFile));

        File.WriteAllText(ExportedFile, "edited in the export folder");

        Assert.Equal("photo-content", File.ReadAllText(SourceFile));
    }

    [Fact]
    public async Task HardLinkMode_SharesTheFileInsteadOfCopyingIt()
    {
        PublishFileResult result = await PublishOnePhotoAsync(PublishLinkMode.HardLinkWhenPossible);

        Assert.True(result.Created);
        Assert.True(result.Linked);
        Assert.True(File.Exists(ExportedFile));

        // Both paths point at the same inode: writing through one is visible
        // through the other. That is the space saving, and the caveat.
        File.WriteAllText(ExportedFile, "edited");

        Assert.Equal("edited", File.ReadAllText(SourceFile));
    }

    [Fact]
    public async Task MissingSource_IsReportedWithoutWritingAnything()
    {
        PublishFileResult result = await PublishOnePhotoAsync(PublishLinkMode.HardLinkWhenPossible, createSourceFile: false);

        Assert.False(result.Created);
        Assert.False(result.Linked);
        Assert.Null(result.Destination);
        Assert.False(File.Exists(ExportedFile));
    }

    [Fact]
    public async Task PublishingTwice_SkipsTheFileAlreadyThere()
    {
        await PublishOnePhotoAsync(PublishLinkMode.HardLinkWhenPossible);

        PublishFileResult second = await PublishOnePhotoAsync(PublishLinkMode.HardLinkWhenPossible);

        Assert.False(second.Created);
        Assert.False(second.Linked);
        Assert.Equal(ExportedFile, second.Destination);
    }
}

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

    private async Task SeedTaggedItemAsync(string tagName, string itemName)
    {
        await using IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(_workingDir);
        unitOfWork.TagRepository.TryCreateTag(tagName, out _);
        CanHaveTag item = new(itemName);
        await unitOfWork.TagRepository.AddOrUpdateItem(item, _ => Task.FromResult("hash"));
        await unitOfWork.SaveChangesAsync();

        Tag tag = unitOfWork.TagRepository.GetAllTag().First(t => t.Name == tagName);
        unitOfWork.TagRepository.AddTagToItem(tag, item);
        await unitOfWork.SaveChangesAsync();
    }

    private async Task<List<PublishFileResult>> PublishAsync(bool dropEverythingFirst = false)
    {
        List<PublishFileResult> results = new();
        await new Publisher().PublishToFolder(
            _workingDir,
            _targetDir,
            dropEverythingFirst,
            PublishLinkMode.Copy,
            _ => { },
            results.Add,
            _ => { },
            _ => { },
            CancellationToken.None);

        return results;
    }

    [Fact]
    public async Task ExportFolderEqualToLibrary_IsRefusedBeforeDeletingAnything()
    {
        File.WriteAllText(SourceFile, "precious");
        await SeedTaggedItemAsync("Maman", "photo.jpg");

        await Assert.ThrowsAsync<InvalidOperationException>(() => new Publisher().PublishToFolder(
            _workingDir,
            _workingDir,
            dropEverythingFirst: true,
            PublishLinkMode.Copy,
            _ => { },
            _ => { },
            _ => { },
            _ => { },
            CancellationToken.None));

        Assert.True(File.Exists(SourceFile));
    }

    [Theory]
    [InlineData("Maman", "Maman")]
    [InlineData("../évasion", ".._évasion")]
    [InlineData("photos/2026", "photos_2026")]
    [InlineData("  dots...  ", "dots")]
    [InlineData("..", "_")]
    [InlineData("", "_")]
    [InlineData("CON", "_CON")]
    public void GetSafeFolderName_StripsAnythingThatWouldEscapeOrBreakTheFilesystem(string tag, string expected)
    {
        Assert.Equal(expected, Publisher.GetSafeFolderName(tag));
    }

    [Fact]
    public async Task TagWithPathSeparators_IsPublishedInsideTheExportFolder()
    {
        File.WriteAllText(SourceFile, "photo-content");
        await SeedTaggedItemAsync("../évasion", "photo.jpg");

        PublishFileResult result = Assert.Single(await PublishAsync());

        Assert.False(result.Failed);
        Assert.True(File.Exists(Path.Combine(_targetDir, ".._évasion", "photo.jpg")));
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "évasion")));
    }

    [Fact]
    public async Task ItemOutsideTheLibrary_IsReportedAndNotPublished()
    {
        string outside = Path.Combine(_tempDir, "outside.jpg");
        File.WriteAllText(outside, "should not leave the library");
        await SeedTaggedItemAsync("Maman", "../outside.jpg");

        PublishFileResult result = Assert.Single(await PublishAsync());

        Assert.True(result.Failed);
        Assert.False(File.Exists(ExportedFile));
    }

    [Fact]
    public async Task OneUnwritableFile_DoesNotStopTheOthers()
    {
        File.WriteAllText(SourceFile, "first");
        File.WriteAllText(Path.Combine(_workingDir, "second.jpg"), "second");
        await SeedTaggedItemAsync("Maman", "photo.jpg");
        await SeedTaggedItemAsync("Maman", "second.jpg");

        // A directory where the export expects a file makes that one copy fail.
        Directory.CreateDirectory(Path.Combine(_targetDir, "Maman", "photo.jpg"));

        List<PublishFileResult> results = await PublishAsync();

        PublishFileResult failed = Assert.Single(results, result => result.Failed);
        Assert.Equal("photo.jpg", Path.GetFileName(failed.Source));
        Assert.Contains(results, result => Path.GetFileName(result.Source) == "second.jpg" && result.Created);
        Assert.True(File.Exists(Path.Combine(_targetDir, "Maman", "second.jpg")));
    }

    [Fact]
    public async Task DropEverythingFirst_LeavesAnEmptyExportFolderBehind()
    {
        Directory.CreateDirectory(_targetDir);
        File.WriteAllText(Path.Combine(_targetDir, "old.txt"), "old");

        List<PublishFileResult> results = await PublishAsync(dropEverythingFirst: true);

        Assert.Empty(results);
        Assert.True(Directory.Exists(_targetDir));
        Assert.Empty(Directory.GetFileSystemEntries(_targetDir));
    }

    [Fact]
    public async Task DropEverythingFirst_DeletesTheLinkButNotWhatItPointsTo()
    {
        string outside = Path.Combine(_tempDir, "outside-folder");
        Directory.CreateDirectory(outside);
        string victim = Path.Combine(outside, "precious.txt");
        File.WriteAllText(victim, "keep me");

        Directory.CreateDirectory(_targetDir);
        string link = Path.Combine(_targetDir, "link");
        try
        {
            Directory.CreateSymbolicLink(link, outside);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Creating a symlink needs developer mode on Windows and a capable
            // filesystem elsewhere; the scenario is optional.
            return;
        }

        await PublishAsync(dropEverythingFirst: true);

        Assert.True(File.Exists(victim));
        Assert.False(Directory.Exists(link));
    }
}

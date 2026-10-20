using DynamicData;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Tests;

public class FolderScanTests : IDisposable
{
    private readonly string _folder;
    private readonly SqliteConnection _connection;
    private readonly InMemoryUnitOfWorkFactory _unitOfWorkFactory;

    public FolderScanTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "ImgTagFanOut-FolderScan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _unitOfWorkFactory = new InMemoryUnitOfWorkFactory(_connection);
    }

    public void Dispose()
    {
        _connection.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private static void AddFile(string folder, string name, string content = "image")
    {
        string path = Path.Combine(folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private FolderScan CreateScan(IHashEvaluator hashEvaluator)
    {
        return new FolderScan(hashEvaluator, _unitOfWorkFactory);
    }

    [Fact]
    public async Task Scan_FindsNestedImagesAndIgnoresOtherFiles()
    {
        AddFile(_folder, "a.jpg");
        AddFile(_folder, "sub/b.PNG");
        AddFile(_folder, "sub/notes.txt");
        AddFile(_folder, "sub/archive.zip");

        SourceList<CanHaveTag> images = new();
        await CreateScan(new FakeHashEvaluator()).ScanFolder(CancellationToken.None, _folder, images);

        Assert.Equal(2, images.Count);
        Assert.Contains(images.Items, image => image.Item == "a.jpg");
        Assert.Contains(images.Items, image => image.Item == Path.Combine("sub", "b.PNG"));

        using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(_folder);
        List<ItemDao> items = unitOfWork.AsDbContext().Items.OrderBy(item => item.Name).ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.False(string.IsNullOrEmpty(item.Hash)));
    }

    [Fact]
    public async Task Scan_WithoutFolder_ThrowsAClearError()
    {
        SourceList<CanHaveTag> images = new();
        string missing = Path.Combine(_folder, "missing");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => CreateScan(new FakeHashEvaluator()).ScanFolder(CancellationToken.None, missing, images));
    }

    [Fact]
    public async Task Scan_WithAnUnreadableFile_KeepsTheOtherFilesAndTheExistingRow()
    {
        AddFile(_folder, "good.jpg");
        AddFile(_folder, "locked.jpg");

        using (IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(_folder))
        {
            unitOfWork.AsDbContext().Tags.Add(new TagDao("cat"));
            unitOfWork.AsDbContext().Items.Add(new ItemDao("locked.jpg") { Hash = "old-hash" });
            unitOfWork.SaveChanges();
            unitOfWork.TagRepository.AddTagToItem(new Tag("cat"), new CanHaveTag("locked.jpg"));
            unitOfWork.SaveChanges();
        }

        FakeHashEvaluator hashEvaluator = new() { ThrowOn = "locked.jpg" };
        SourceList<CanHaveTag> images = new();

        await CreateScan(hashEvaluator).ScanFolder(CancellationToken.None, _folder, images);

        Assert.Equal(2, images.Count);
        Assert.Contains(hashEvaluator.Calls, call => call.EndsWith("good.jpg", StringComparison.Ordinal));

        using IUnitOfWork readable = _unitOfWorkFactory.GetUnitOfWork(_folder);
        ImgTagFanOutDbContext db = readable.AsDbContext();

        ItemDao locked = db.Items.Include(item => item.Tags).Single(item => item.Name == "locked.jpg");
        Assert.Equal("old-hash", locked.Hash);
        Assert.Single(locked.Tags);

        ItemDao good = db.Items.Single(item => item.Name == "good.jpg");
        Assert.False(string.IsNullOrEmpty(good.Hash));
    }

    [Fact]
    public async Task Scan_CancelledHalfway_KeepsWhatWasAlreadyRead()
    {
        AddFile(_folder, "a.jpg");
        AddFile(_folder, "b.jpg");

        using CancellationTokenSource cancellation = new();
        FakeHashEvaluator hashEvaluator = new() { CancelAfterCall = 1, Cancellation = cancellation };
        SourceList<CanHaveTag> images = new();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateScan(hashEvaluator).ScanFolder(cancellation.Token, _folder, images));

        Assert.Empty(images.Items);

        using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(_folder);
        Assert.Equal("a.jpg", Assert.Single(unitOfWork.AsDbContext().Items).Name);
    }

    private sealed class FakeHashEvaluator : IHashEvaluator
    {
        private int _calls;

        public string? ThrowOn { get; init; }

        public int CancelAfterCall { get; init; }

        public CancellationTokenSource? Cancellation { get; init; }

        public List<string> Calls { get; } = new();

        public Task<string> ComputeHashAsync(string filePath, CancellationToken ctsToken)
        {
            Calls.Add(filePath);
            _calls++;

            if (ThrowOn is not null && Path.GetFileName(filePath) == ThrowOn)
            {
                throw new IOException($"cannot read {ThrowOn}");
            }

            if (CancelAfterCall > 0 && _calls >= CancelAfterCall)
            {
                Cancellation?.Cancel();
            }

            return Task.FromResult("hash:" + Path.GetFileName(filePath));
        }
    }
}

using DynamicData;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.ViewModels;
using Microsoft.Data.Sqlite;

namespace ImgTagFanOut.Tests;

/// <summary>
/// A folder scan must not read every file again: the hash is the cache key for
/// the face analysis, but computing it costs a full read, which dominates a
/// rescan of a large library.
/// </summary>
public class FolderScanCacheTests : IDisposable
{
    private readonly string _folder;
    private readonly SqliteConnection _connection;
    private readonly InMemoryUnitOfWorkFactory _unitOfWorkFactory;

    public FolderScanCacheTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "ImgTagFanOut-Scan-" + Guid.NewGuid().ToString("N"));
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

    private void WritePhoto(string name, byte[] content, DateTime? lastWrite = null)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, content);
        if (lastWrite is not null)
        {
            File.SetLastWriteTimeUtc(path, lastWrite.Value);
        }
    }

    private async Task<List<CanHaveTag>> ScanAsync(CountingHashEvaluator evaluator)
    {
        FolderScan scan = new(evaluator, _unitOfWorkFactory);
        SourceList<CanHaveTag> images = new();

        await scan.ScanFolder(CancellationToken.None, _folder, images);

        return images.Items.ToList();
    }

    [Fact]
    public async Task RescanningAnUntouchedFolder_DoesNotReadTheFilesAgain()
    {
        WritePhoto("a.jpg", [1, 2, 3], new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        WritePhoto("b.jpg", [4, 5, 6], new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        CountingHashEvaluator evaluator = new();

        await ScanAsync(evaluator);
        Assert.Equal(2, evaluator.Calls);

        await ScanAsync(evaluator);
        Assert.Equal(2, evaluator.Calls);
    }

    [Fact]
    public async Task ChangingAFile_HashesOnlyThatFileAgain()
    {
        DateTime first = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        WritePhoto("a.jpg", [1, 2, 3], first);
        WritePhoto("b.jpg", [4, 5, 6], first);

        CountingHashEvaluator evaluator = new();
        await ScanAsync(evaluator);
        Assert.Equal(2, evaluator.Calls);

        WritePhoto("a.jpg", [9, 9, 9, 9], first.AddMinutes(5));

        await ScanAsync(evaluator);
        Assert.Equal(3, evaluator.Calls);
    }

    [Fact]
    public async Task Rescanning_KeepsTheTagsOfUnchangedFiles()
    {
        DateTime written = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        WritePhoto("a.jpg", [1, 2, 3], written);

        using (IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(_folder))
        {
            unitOfWork.TagRepository.TryCreateTag("cat", out _);
            unitOfWork.SaveChanges();

            CanHaveTag item = new("a.jpg") { Size = 3, ModifiedAtUtcTicks = written.Ticks };
            await unitOfWork.TagRepository.AddOrUpdateItem(item, _ => Task.FromResult("hash:3:1"));
            await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
            unitOfWork.TagRepository.AddTagToItem(new Tag("cat"), item);
            await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Le scan suivant doit retrouver le tag sans relire le fichier.
        CountingHashEvaluator evaluator = new();
        List<CanHaveTag> images = await ScanAsync(evaluator);

        Assert.Equal(0, evaluator.Calls);
        Assert.True(Assert.Single(images).Has(new Tag("cat")));
    }

    [Fact]
    public async Task ChangingOnlyTheLastWriteTime_IsEnoughToRehash()
    {
        DateTime first = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        WritePhoto("a.jpg", [1, 2, 3], first);

        CountingHashEvaluator evaluator = new();
        await ScanAsync(evaluator);

        // Même contenu, même taille, date différente : on ne peut pas prouver
        // que le fichier n'a pas changé, donc on le relit.
        File.SetLastWriteTimeUtc(Path.Combine(_folder, "a.jpg"), first.AddDays(1));

        await ScanAsync(evaluator);

        Assert.Equal(2, evaluator.Calls);
    }

    private sealed class CountingHashEvaluator : IHashEvaluator
    {
        public int Calls { get; private set; }

        public Task<string> ComputeHashAsync(string filePath, CancellationToken ctsToken)
        {
            Calls++;
            byte[] content = File.ReadAllBytes(filePath);
            return Task.FromResult($"hash:{content.Length}:{(content.Length > 0 ? content[0] : -1)}");
        }
    }
}

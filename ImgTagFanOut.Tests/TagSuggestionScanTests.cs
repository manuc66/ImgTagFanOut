using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.Models.Suggestions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Tests;

public class TagSuggestionScanTests : IDisposable
{
    private readonly string _folder;
    private readonly SqliteConnection _connection;
    private readonly InMemoryUnitOfWorkFactory _factory;
    private readonly FakeHashEvaluator _hashEvaluator = new();

    public TagSuggestionScanTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "ImgTagFanOut-TagScan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _factory = new InMemoryUnitOfWorkFactory(_connection);

        using IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder);
    }

    public void Dispose()
    {
        _connection.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private void AddImage(string name)
    {
        FakeImageGenerator.WritePng(Path.GetDirectoryName(Path.Combine(_folder, name))!, Path.GetFileName(name));
    }

    private ItemDao RegisterItem(string name, string? hash = null, params string[] tags)
    {
        using IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder);
        ImgTagFanOutDbContext db = unitOfWork.AsDbContext();

        ItemDao item = new(name) { Hash = hash };
        db.Items.Add(item);
        db.SaveChanges();

        for (int i = 0; i < tags.Length; i++)
        {
            TagDao? tag = db.Tags.FirstOrDefault(row => row.Name == tags[i]);
            if (tag is null)
            {
                tag = new TagDao(tags[i]);
                db.Tags.Add(tag);
                db.SaveChanges();
            }

            db.ItemTags.Add(new ItemTagDao
            {
                ItemForeignKey = item.ItemId,
                TagForeignKey = tag.TagId,
                OrderIndex = i,
            });
        }

        db.SaveChanges();
        return item;
    }

    private void RegisterTag(string name)
    {
        using IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder);
        unitOfWork.AsDbContext().Tags.Add(new TagDao(name));
        unitOfWork.SaveChanges();
    }

    private void RegisterEmbedding(int itemId, params float[] embedding)
    {
        using IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder);
        unitOfWork.SuggestionRepository.SaveEmbedding(itemId, "gallery-hash", "fake", embedding);
        unitOfWork.SaveChanges();
    }

    private TagSuggestionScan CreateScan(Func<IImageEmbedder>? embedderFactory = null)
    {
        return new TagSuggestionScan(
            _factory,
            _hashEvaluator,
            embedderFactory ?? (() => new FakeImageEmbedder()),
            new TagSuggestionEngine());
    }

    private IReadOnlyList<TagSuggestionDao> Suggestions(string itemName)
    {
        using IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder);
        return unitOfWork.SuggestionRepository.GetSuggestions(itemName);
    }

    [Fact]
    public async Task Scan_ProposesTheFolderName_AndCachesTheDescriptor()
    {
        AddImage("2024/Vacances/IMG_1.jpg");
        ItemDao item = RegisterItem("2024/Vacances/IMG_1.jpg");
        RegisterTag("Vacances");
        RegisterTag("Travail");

        FakeImageEmbedder embedder = new();
        TagSuggestionScanSummary summary = await CreateScan(() => embedder)
            .ScanAsync(_folder, null, CancellationToken.None);

        Assert.Equal(1, summary.Total);
        Assert.Equal(1, summary.SuggestedItems);
        Assert.Equal(1, summary.ComputedEmbeddings);
        Assert.False(summary.EmbedderUnavailable);

        using IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder);
        TagSuggestionDao suggestion = Assert.Single(unitOfWork.SuggestionRepository.GetSuggestions(item.Name));
        Assert.Equal("Vacances", suggestion.TagName);
        Assert.Equal(TagSuggestionSource.Path, suggestion.Source);
        Assert.Equal(TagSuggestionState.Pending, suggestion.State);
        Assert.NotNull(unitOfWork.SuggestionRepository.GetEmbedding(item.ItemId));
    }

    [Fact]
    public async Task SecondScan_ReusesTheCachedDescriptor()
    {
        AddImage("vacances/a.jpg");
        RegisterItem("vacances/a.jpg");
        RegisterTag("Vacances");

        FakeImageEmbedder first = new();
        await CreateScan(() => first).ScanAsync(_folder, null, CancellationToken.None);

        FakeImageEmbedder second = new();
        TagSuggestionScanSummary summary = await CreateScan(() => second)
            .ScanAsync(_folder, null, CancellationToken.None);

        Assert.Equal(0, summary.ComputedEmbeddings);
        Assert.Equal(1, summary.CachedEmbeddings);
        Assert.Equal(0, second.Calls);
    }

    [Fact]
    public async Task ChangedHash_InvalidatesTheCachedDescriptor()
    {
        AddImage("vacances/a.jpg");
        ItemDao item = RegisterItem("vacances/a.jpg");
        RegisterTag("Vacances");

        await CreateScan().ScanAsync(_folder, null, CancellationToken.None);

        using (IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder))
        {
            ItemDao stored = unitOfWork.AsDbContext().Items.Single(row => row.ItemId == item.ItemId);
            stored.Hash = "a-new-hash";
            unitOfWork.SaveChanges();
        }

        FakeImageEmbedder embedder = new();
        TagSuggestionScanSummary summary = await CreateScan(() => embedder)
            .ScanAsync(_folder, null, CancellationToken.None);

        Assert.Equal(1, summary.ComputedEmbeddings);
        Assert.Equal(0, summary.CachedEmbeddings);
    }

    [Fact]
    public async Task MissingFile_IsSkipped()
    {
        AddImage("present.jpg");
        RegisterItem("present.jpg");
        RegisterItem("gone.jpg");
        RegisterTag("Vacances");

        TagSuggestionScanSummary summary = await CreateScan().ScanAsync(_folder, null, CancellationToken.None);

        Assert.Equal(2, summary.Total);
        Assert.Equal(1, summary.Skipped);
    }

    [Fact]
    public async Task MissingModel_StillUsesThePathAndFaceSignals()
    {
        AddImage("vacances/a.jpg");
        ItemDao item = RegisterItem("vacances/a.jpg");
        RegisterTag("Vacances");

        TagSuggestionScanSummary summary = await CreateScan(() => throw new FileNotFoundException("no model"))
            .ScanAsync(_folder, null, CancellationToken.None);

        Assert.True(summary.EmbedderUnavailable);
        TagSuggestionDao suggestion = Assert.Single(Suggestions(item.Name));
        Assert.Equal("Vacances", suggestion.TagName);
    }

    [Fact]
    public async Task RejectedSuggestion_IsNotProposedAgain()
    {
        AddImage("vacances/a.jpg");
        ItemDao item = RegisterItem("vacances/a.jpg");
        RegisterTag("Vacances");

        using (IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder))
        {
            unitOfWork.SuggestionRepository.ReplacePending(item.ItemId, "hash", "v1",
                [new TagSuggestionRecord("Vacances", 0.8, TagSuggestionSource.Path, "a.jpg")]);
            unitOfWork.SaveChanges();
            unitOfWork.SuggestionRepository.SetState(item.Name, "Vacances", TagSuggestionState.Rejected);
            unitOfWork.SaveChanges();
        }

        await CreateScan().ScanAsync(_folder, null, CancellationToken.None);

        TagSuggestionDao suggestion = Assert.Single(Suggestions(item.Name));
        Assert.Equal(TagSuggestionState.Rejected, suggestion.State);
    }

    [Fact]
    public async Task Scan_ProposesATagFromSimilarTaggedPhotos()
    {
        AddImage("target.jpg");
        ItemDao target = RegisterItem("target.jpg");

        RegisterTag("Plage");
        RegisterTag("Montagne");
        RegisterTag("Bureau");

        (string Tag, float[] Vector)[] examples =
        [
            ("Plage", [1f, 0.01f, 0f]),
            ("Plage", [1f, 0f, 0.02f]),
            ("Montagne", [0.5f, 1f, 0f]),
            ("Montagne", [0.4f, 1f, 0f]),
            ("Bureau", [0.2f, 0.1f, 1f]),
            ("Bureau", [0.2f, 0.2f, 1f]),
        ];

        for (int i = 0; i < examples.Length; i++)
        {
            ItemDao galleryItem = RegisterItem($"gallery-{i}.jpg", null, examples[i].Tag);
            RegisterEmbedding(galleryItem.ItemId, examples[i].Vector);
        }

        FakeImageEmbedder embedder = new() { Vectors = { ["target.jpg"] = [1f, 0.02f, 0f] } };
        TagSuggestionScanSummary summary = await CreateScan(() => embedder)
            .ScanAsync(_folder, null, CancellationToken.None);

        TagSuggestionDao suggestion = Assert.Single(Suggestions(target.Name));
        Assert.Equal("Plage", suggestion.TagName);
        Assert.Equal(TagSuggestionSource.Similar, suggestion.Source);
        Assert.Equal(TagSuggestionState.Pending, suggestion.State);
    }

    [Fact]
    public async Task SimilarityDoesNotDependOnFileOrder()
    {
        // The untagged photo sorts first: the scan must still build the gallery
        // from the tagged photos before scoring it.
        AddImage("aaa-target.jpg");
        ItemDao target = RegisterItem("aaa-target.jpg");

        RegisterTag("Plage");
        RegisterTag("Montagne");
        RegisterTag("Bureau");

        (string Tag, float[] Vector)[] examples =
        [
            ("Plage", [1f, 0.01f, 0f]),
            ("Plage", [1f, 0f, 0.02f]),
            ("Montagne", [0.5f, 1f, 0f]),
            ("Montagne", [0.4f, 1f, 0f]),
            ("Bureau", [0.2f, 0.1f, 1f]),
            ("Bureau", [0.2f, 0.2f, 1f]),
        ];

        for (int i = 0; i < examples.Length; i++)
        {
            ItemDao galleryItem = RegisterItem($"zzz-gallery-{i}.jpg", null, examples[i].Tag);
            RegisterEmbedding(galleryItem.ItemId, examples[i].Vector);
        }

        FakeImageEmbedder embedder = new() { Vectors = { ["aaa-target.jpg"] = [1f, 0.02f, 0f] } };
        await CreateScan(() => embedder).ScanAsync(_folder, null, CancellationToken.None);

        TagSuggestionDao suggestion = Assert.Single(Suggestions(target.Name));
        Assert.Equal("Plage", suggestion.TagName);
    }

    [Fact]
    public async Task OneBrokenImage_DoesNotStopTheOthers()
    {
        AddImage("vacances/good.jpg");
        AddImage("vacances/broken.jpg");
        ItemDao good = RegisterItem("vacances/good.jpg");
        RegisterItem("vacances/broken.jpg");
        RegisterTag("Vacances");

        FakeImageEmbedder embedder = new() { ThrowOn = "broken.jpg" };
        TagSuggestionScanSummary summary = await CreateScan(() => embedder)
            .ScanAsync(_folder, null, CancellationToken.None);

        Assert.Equal(1, summary.Failures);
        Assert.Single(Suggestions(good.Name));
    }

    [Fact]
    public async Task EmptyLibrary_DoesNothing()
    {
        TagSuggestionScanSummary summary = await CreateScan().ScanAsync(_folder, null, CancellationToken.None);

        Assert.Equal(0, summary.Total);
        Assert.Equal(0, summary.SuggestionCount);
    }

    private sealed class FakeHashEvaluator : IHashEvaluator
    {
        public Task<string> ComputeHashAsync(string filePath, CancellationToken ctsToken)
        {
            string name = Path.GetFileName(filePath);
            return Task.FromResult($"hash-{name}");
        }
    }

    private sealed class FakeImageEmbedder : IImageEmbedder
    {
        public string Version => "fake/v1";

        public int Calls { get; private set; }

        public string? ThrowOn { get; init; }

        public Dictionary<string, float[]> Vectors { get; } = new(StringComparer.OrdinalIgnoreCase);

        public float[] Embed(string fullPath)
        {
            Calls++;
            string name = Path.GetFileName(fullPath);
            if (string.Equals(name, ThrowOn, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("broken image");
            }

            return Vectors.TryGetValue(name, out float[]? vector) ? vector : [1f, 0f, 0f];
        }

        public void Dispose()
        {
        }
    }
}

using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.Models.Suggestions;
using Microsoft.Data.Sqlite;

namespace ImgTagFanOut.Tests;

/// <summary>
/// Drives the real ONNX model through the whole scan on the demo portraits:
/// the only way to know the descriptor and the kNN threshold agree on real
/// photos, not just on synthetic vectors.
/// </summary>
public class TagSuggestionEndToEndTests : IDisposable
{
    private readonly string _folder;
    private readonly SqliteConnection _connection;
    private readonly InMemoryUnitOfWorkFactory _factory;

    public TagSuggestionEndToEndTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "ImgTagFanOut-TagE2E-" + Guid.NewGuid().ToString("N"));
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

    private void CopyDemoPhoto(string person, string photo, string targetName)
    {
        string source = Path.Combine(AppContext.BaseDirectory, "demo", "faces", person, photo);
        File.Copy(source, Path.Combine(_folder, targetName));
    }

    private ItemDao RegisterItem(string name, params string[] tags)
    {
        using IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder);
        ImgTagFanOutDbContext db = unitOfWork.AsDbContext();

        ItemDao item = new(name);
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

    [Fact]
    public async Task RealModel_RecognisesTheSamePersonAmongTaggedPhotos()
    {
        CopyDemoPhoto("obama", "photo-1.jpg", "query.jpg");
        CopyDemoPhoto("obama", "photo-2.jpg", "obama.jpg");
        CopyDemoPhoto("reagan", "photo-1.jpg", "reagan-1.jpg");
        CopyDemoPhoto("reagan", "photo-2.jpg", "reagan-2.jpg");
        CopyDemoPhoto("bush-jr", "photo-1.jpg", "bush-1.jpg");
        CopyDemoPhoto("bush-jr", "photo-2.jpg", "bush-2.jpg");

        RegisterItem("obama.jpg", "Obama");
        RegisterItem("reagan-1.jpg", "Reagan");
        RegisterItem("reagan-2.jpg", "Reagan");
        RegisterItem("bush-1.jpg", "Bush");
        RegisterItem("bush-2.jpg", "Bush");
        ItemDao query = RegisterItem("query.jpg");

        TagSuggestionScan scan = new(_factory, new FakeHashEvaluator(), () => new OnnxImageEmbedder(), new TagSuggestionEngine());
        TagSuggestionScanSummary summary = await scan.ScanAsync(_folder, null, CancellationToken.None);

        Assert.False(summary.EmbedderUnavailable);
        Assert.True(summary.ComputedEmbeddings >= 6);

        using IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder);
        IReadOnlyList<TagSuggestionDao> suggestions = unitOfWork.SuggestionRepository.GetSuggestions(query.Name);

        Assert.Contains(suggestions, suggestion => suggestion.TagName == "Obama" && suggestion.State == TagSuggestionState.Pending);
    }

    private sealed class FakeHashEvaluator : IHashEvaluator
    {
        public Task<string> ComputeHashAsync(string filePath, CancellationToken ctsToken)
        {
            return Task.FromResult($"hash-{Path.GetFileName(filePath)}");
        }
    }
}

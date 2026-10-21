using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.Models.Suggestions;
using ImgTagFanOut.ViewModels;
using Microsoft.Data.Sqlite;

namespace ImgTagFanOut.Tests;

public class SuggestionRepositoryTests : IDisposable
{
    private readonly string _folder;
    private readonly SqliteConnection _connection;
    private readonly InMemoryUnitOfWorkFactory _factory;

    public SuggestionRepositoryTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "ImgTagFanOut-Suggestions-" + Guid.NewGuid().ToString("N"));
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

    private ItemDao AddItem(string name, params string[] tags)
    {
        using IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder);
        ImgTagFanOutDbContext db = unitOfWork.AsDbContext();

        ItemDao item = new(name);
        db.Items.Add(item);
        db.SaveChanges();

        for (int i = 0; i < tags.Length; i++)
        {
            string name_ = tags[i];
            TagDao? tag = db.Tags.FirstOrDefault(row => row.Name == name_);
            if (tag is null)
            {
                tag = new TagDao(name_);
                db.Tags.Add(tag);
                db.SaveChanges();
            }

            // Insert the join row directly: AddTagToItem also fixes up the skip
            // navigations, which is not what this repository test is about.
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
    public void ReplacePending_PersistsTheSuggestions()
    {
        ItemDao item = AddItem("a.jpg");

        using (IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder))
        {
            unitOfWork.SuggestionRepository.ReplacePending(item.ItemId, "hash", "v1",
            [
                new TagSuggestionRecord("Vacances", 0.8, TagSuggestionSource.Path, "a.jpg"),
                new TagSuggestionRecord("Famille", 0.6, TagSuggestionSource.Similar, "3"),
            ]);
            unitOfWork.SaveChanges();
        }

        using IUnitOfWork readable = _factory.GetUnitOfWork(_folder);
        IReadOnlyList<TagSuggestionDao> rows = readable.SuggestionRepository.GetSuggestions("a.jpg");

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(TagSuggestionState.Pending, row.State));
        Assert.Equal("Vacances", rows[0].TagName);
        Assert.Equal(0.8, rows[0].Score, 3);
    }

    [Fact]
    public void ReplacePending_KeepsRejectedAndAcceptedHistory()
    {
        ItemDao item = AddItem("a.jpg");

        using (IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder))
        {
            unitOfWork.SuggestionRepository.ReplacePending(item.ItemId, "hash", "v1",
            [
                new TagSuggestionRecord("Vacances", 0.8, TagSuggestionSource.Path, "a.jpg"),
                new TagSuggestionRecord("Famille", 0.6, TagSuggestionSource.Path, "a.jpg"),
            ]);
            unitOfWork.SaveChanges();
            unitOfWork.SuggestionRepository.SetState("a.jpg", "Vacances", TagSuggestionState.Rejected);
            unitOfWork.SuggestionRepository.SetState("a.jpg", "Famille", TagSuggestionState.Accepted);
            unitOfWork.SaveChanges();
        }

        using (IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder))
        {
            unitOfWork.SuggestionRepository.ReplacePending(item.ItemId, "hash", "v2",
            [
                new TagSuggestionRecord("Vacances", 0.9, TagSuggestionSource.Path, "a.jpg"),
                new TagSuggestionRecord("Travail", 0.7, TagSuggestionSource.Path, "a.jpg"),
            ]);
            unitOfWork.SaveChanges();
        }

        using IUnitOfWork readable = _factory.GetUnitOfWork(_folder);
        List<TagSuggestionDao> rows = readable.SuggestionRepository.GetSuggestions("a.jpg").ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal(TagSuggestionState.Rejected, rows.Single(row => row.TagName == "Vacances").State);
        Assert.Equal(TagSuggestionState.Accepted, rows.Single(row => row.TagName == "Famille").State);
        Assert.Equal(TagSuggestionState.Pending, rows.Single(row => row.TagName == "Travail").State);
    }

    [Fact]
    public void RejectedTags_AreReportedAndNotOverwritten()
    {
        ItemDao item = AddItem("a.jpg");

        using (IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder))
        {
            unitOfWork.SuggestionRepository.ReplacePending(item.ItemId, "hash", "v1",
                [new TagSuggestionRecord("Vacances", 0.8, TagSuggestionSource.Path, "a.jpg")]);
            unitOfWork.SaveChanges();
            unitOfWork.SuggestionRepository.SetState("a.jpg", "Vacances", TagSuggestionState.Rejected);
            unitOfWork.SaveChanges();
        }

        using (IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder))
        {
            Assert.Equal(["Vacances"], unitOfWork.SuggestionRepository.GetRejectedTags("a.jpg"));

            unitOfWork.SuggestionRepository.ReplacePending(item.ItemId, "hash", "v1",
                [new TagSuggestionRecord("Vacances", 0.95, TagSuggestionSource.Face, "Maman")]);
            unitOfWork.SaveChanges();
        }

        using IUnitOfWork readable = _factory.GetUnitOfWork(_folder);
        TagSuggestionDao row = Assert.Single(readable.SuggestionRepository.GetSuggestions("a.jpg"));
        Assert.Equal(TagSuggestionState.Rejected, row.State);
        Assert.Equal(0.8, row.Score, 3);
    }

    [Fact]
    public void ReplacePending_ReopensAnAcceptedSuggestionInsteadOfDuplicatingIt()
    {
        ItemDao item = AddItem("a.jpg");

        using (IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder))
        {
            unitOfWork.SuggestionRepository.ReplacePending(item.ItemId, "hash", "v1",
                [new TagSuggestionRecord("Vacances", 0.8, TagSuggestionSource.Path, "a.jpg")]);
            unitOfWork.SaveChanges();
            unitOfWork.SuggestionRepository.SetState("a.jpg", "Vacances", TagSuggestionState.Accepted);
            unitOfWork.SaveChanges();
        }

        // The user removed the tag from the photo: the engine proposes it again.
        using (IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder))
        {
            unitOfWork.SuggestionRepository.ReplacePending(item.ItemId, "hash", "v2",
                [new TagSuggestionRecord("Vacances", 0.9, TagSuggestionSource.Face, "Maman")]);
            unitOfWork.SaveChanges();
        }

        using IUnitOfWork readable = _factory.GetUnitOfWork(_folder);
        TagSuggestionDao row = Assert.Single(readable.SuggestionRepository.GetSuggestions("a.jpg"));
        Assert.Equal(TagSuggestionState.Pending, row.State);
        Assert.Equal(0.9, row.Score, 3);
        Assert.Equal("v2", row.EngineVersion);
    }

    [Fact]
    public void Embedding_RoundTripsAndIsReplaced()
    {
        ItemDao item = AddItem("a.jpg");

        using (IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder))
        {
            unitOfWork.SuggestionRepository.SaveEmbedding(item.ItemId, "hash-1", "model-1", [0.6f, 0.8f, 0f]);
            unitOfWork.SaveChanges();
        }

        using (IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder))
        {
            ItemEmbeddingDao stored = Assert.IsType<ItemEmbeddingDao>(
                unitOfWork.SuggestionRepository.GetEmbedding(item.ItemId));
            Assert.Equal("hash-1", stored.SourceHash);
            Assert.Equal("model-1", stored.ModelVersion);
            Assert.Equal(3, stored.Dimensions);
            Assert.Equal([0.6f, 0.8f, 0f], SuggestionRepository.ToFloats(stored.Embedding));
        }

        using (IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder))
        {
            unitOfWork.SuggestionRepository.SaveEmbedding(item.ItemId, "hash-2", "model-2", [1f, 0f]);
            unitOfWork.SaveChanges();
        }

        using IUnitOfWork readable = _factory.GetUnitOfWork(_folder);
        ItemEmbeddingDao updated = Assert.IsType<ItemEmbeddingDao>(
            readable.SuggestionRepository.GetEmbedding(item.ItemId));
        Assert.Equal("hash-2", updated.SourceHash);
        Assert.Equal(2, updated.Dimensions);
    }

    [Fact]
    public void LabelledEmbeddings_OnlyIncludeTaggedImages()
    {
        ItemDao tagged = AddItem("tagged.jpg", "Vacances");
        ItemDao untagged = AddItem("untagged.jpg");

        using (IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder))
        {
            unitOfWork.SuggestionRepository.SaveEmbedding(tagged.ItemId, "h1", "m", [1f, 0f]);
            unitOfWork.SuggestionRepository.SaveEmbedding(untagged.ItemId, "h2", "m", [0f, 1f]);
            unitOfWork.SaveChanges();
        }

        using IUnitOfWork readable = _factory.GetUnitOfWork(_folder);
        LabelledEmbedding row = Assert.Single(readable.SuggestionRepository.GetLabelledEmbeddings());

        Assert.Equal(tagged.ItemId, row.ItemId);
        Assert.Equal(["Vacances"], row.Tags);
        Assert.Equal([1f, 0f], row.Embedding);
    }

    [Fact]
    public void PendingItemIds_AreDistinct()
    {
        ItemDao first = AddItem("a.jpg");
        ItemDao second = AddItem("b.jpg");

        using (IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder))
        {
            unitOfWork.SuggestionRepository.ReplacePending(first.ItemId, "h", "v",
            [
                new TagSuggestionRecord("Vacances", 0.8, TagSuggestionSource.Path, "a.jpg"),
                new TagSuggestionRecord("Famille", 0.7, TagSuggestionSource.Path, "a.jpg"),
            ]);
            unitOfWork.SuggestionRepository.ReplacePending(second.ItemId, "h", "v",
                [new TagSuggestionRecord("Travail", 0.6, TagSuggestionSource.Path, "b.jpg")]);
            unitOfWork.SaveChanges();
            unitOfWork.SuggestionRepository.SetState("b.jpg", "Travail", TagSuggestionState.Rejected);
            unitOfWork.SaveChanges();
        }

        using IUnitOfWork readable = _factory.GetUnitOfWork(_folder);
        Assert.Equal([first.ItemId], readable.SuggestionRepository.GetItemIdsWithPendingSuggestions());
    }
}

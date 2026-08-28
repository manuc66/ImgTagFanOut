using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Tests;

public class TagRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ImgTagFanOutDbContext _dbContext;
    private readonly TagRepository _repository;

    public TagRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        DbContextOptions<ImgTagFanOutDbContext> options = new DbContextOptionsBuilder<ImgTagFanOutDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ImgTagFanOutDbContext(options, new TagCache());
        _dbContext.Database.EnsureCreated();
        _repository = new TagRepository(_dbContext, new TagCache());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private void CreateTag(string tagName)
    {
        _dbContext.Tags.Add(new TagDao(tagName));
        _dbContext.SaveChanges();
    }

    private void CreateItem(string itemName)
    {
        _dbContext.Items.Add(new ItemDao(itemName));
        _dbContext.SaveChanges();
    }

    [Fact]
    public void AddTagToItem_SameTagTwice_DoesNotDuplicateTagAssociation()
    {
        CreateTag("cat");
        CreateItem("photo.jpg");
        CanHaveTag item = new("photo.jpg");

        _repository.AddTagToItem(new Tag("cat"), item);
        _repository.AddTagToItem(new Tag("cat"), item);
        _dbContext.SaveChanges();

        Assert.Equal(1, _dbContext.Items.Include(i => i.Tags).Single().Tags.Count);
        Assert.Equal(1, _dbContext.ItemTags.Count());
        Assert.Equal(1, item.Tags.Count);
    }

    [Fact]
    public void RemoveTagToItem_RemovesAssociationAndReindexesRemainingTags()
    {
        CreateTag("cat");
        CreateTag("dog");
        CreateTag("bird");
        CreateItem("photo.jpg");
        CanHaveTag item = new("photo.jpg");

        _repository.AddTagToItem(new Tag("cat"), item);
        _repository.AddTagToItem(new Tag("dog"), item);
        _repository.AddTagToItem(new Tag("bird"), item);
        _dbContext.SaveChanges();

        _repository.RemoveTagToItem(new Tag("dog"), item);
        _dbContext.SaveChanges();

        ItemDao stored = _dbContext.Items.Include(i => i.ItemTags).Single();
        Assert.Equal(2, stored.ItemTags.Count);
        Assert.DoesNotContain(stored.ItemTags, x => x.Tag.Name == "dog");
        Assert.Equal(new[] { 0, 1 }, stored.ItemTags.OrderBy(x => x.OrderIndex).Select(x => x.OrderIndex).ToArray());
    }

    [Fact]
    public void AddTagToItem_ThenRemoveTagToItem_ReturnsToEmptyState()
    {
        CreateTag("cat");
        CreateItem("photo.jpg");
        CanHaveTag item = new("photo.jpg");

        _repository.AddTagToItem(new Tag("cat"), item);
        _dbContext.SaveChanges();
        _repository.RemoveTagToItem(new Tag("cat"), item);
        _dbContext.SaveChanges();

        ItemDao stored = _dbContext.Items.Include(i => i.ItemTags).Include(i => i.Tags).Single();
        Assert.Empty(stored.ItemTags);
        Assert.Empty(stored.Tags);
        Assert.Empty(item.Tags);
    }
}
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Tests;

public class TagAndParameterTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ImgTagFanOutDbContext _db;

    public TagAndParameterTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        DbContextOptions<ImgTagFanOutDbContext> options = new DbContextOptionsBuilder<ImgTagFanOutDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new ImgTagFanOutDbContext(options, new TagCache());
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void Tag_EqualityIgnoresCaseAndFilterIsCaseInsensitive()
    {
        Tag tag = new("Maman");

        Assert.Equal(tag, new Tag("maman"));
        Assert.Equal(tag.GetHashCode(), new Tag("MAMAN").GetHashCode());
        Assert.True(tag.Same("maman"));
        Assert.True(tag.MatchFilter("AMA"));
        Assert.False(tag.Same("Papa"));
    }

    [Fact]
    public void CanHaveTag_AddTwiceKeepsOneLabelAndRemoveIsIdempotent()
    {
        Tag cat = new("cat");
        CanHaveTag item = new("sub/photo.jpg");

        item.AddTag(cat);
        item.AddTag(new Tag("CAT"));

        Assert.Single(item.Tags);
        Assert.True(item.Has(cat));

        item.RemoveTag(cat);
        item.RemoveTag(cat);

        Assert.Empty(item.Tags);
    }

    [Fact]
    public void CanHaveTag_ToggleAddsThenRemoves()
    {
        CanHaveTag item = new("photo.jpg");
        Tag cat = new("cat");

        item.Toggle(cat);
        Assert.True(item.Has(cat));

        item.Toggle(new Tag("CAT"));
        Assert.False(item.Has(cat));
    }

    [Fact]
    public void CanHaveTag_GetFullFilePath_CombinesTheLibraryAndTheRelativeItem()
    {
        CanHaveTag item = new(Path.Combine("people", "photo.jpg"));

        Assert.Equal(
            Path.Combine("library", "people", "photo.jpg"),
            item.GetFullFilePath("library"));
    }

    [Fact]
    public void ParameterRepository_Update_InsertsThenUpdatesOneRow()
    {
        ParameterRepository repository = new(_db);

        repository.Update("mode", "copy");
        _db.SaveChanges();
        repository.Update("mode", "link");
        _db.SaveChanges();

        Assert.Equal("link", repository.Get("mode"));
        Assert.Single(_db.Parameters);
    }

    [Fact]
    public void ParameterRepository_Get_WithAConverter_ReadsTheStoredValue()
    {
        ParameterRepository repository = new(_db);
        repository.Update("show_done", "True");
        _db.SaveChanges();

        Assert.Equal("true", repository.Get<string>("show_done", value => value?.ToLowerInvariant()));
        Assert.Null(repository.Get<string>("missing", _ => null));
    }

    [Fact]
    public void ParameterRepository_UpdateWithAConverter_StoresTheConvertedValue()
    {
        ParameterRepository repository = new(_db);

        repository.Update("count", 42, value => value.ToString());
        _db.SaveChanges();

        Assert.Equal("42", Assert.Single(_db.Parameters).Value);
    }
}

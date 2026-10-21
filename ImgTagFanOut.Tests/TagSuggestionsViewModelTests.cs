using System.Reactive.Linq;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.Models.Suggestions;
using ImgTagFanOut.ViewModels;
using Microsoft.Data.Sqlite;

namespace ImgTagFanOut.Tests;

public class TagSuggestionsViewModelTests : IDisposable
{
    private readonly string _folder;
    private readonly SqliteConnection _connection;
    private readonly InMemoryUnitOfWorkFactory _factory;

    public TagSuggestionsViewModelTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "ImgTagFanOut-SuggestionVm-" + Guid.NewGuid().ToString("N"));
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

        ItemDao item = new(name) { Hash = $"hash-{name}" };
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

    private void AddSuggestions(ItemDao item, params (string TagName, TagSuggestionState State)[] rows)
    {
        using IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder);
        unitOfWork.SuggestionRepository.ReplacePending(
            item.ItemId,
            item.Hash!,
            "v1",
            rows.Select(row => new TagSuggestionRecord(row.TagName, 0.8, TagSuggestionSource.Path, item.Name)).ToArray());
        unitOfWork.SaveChanges();

        foreach ((string tagName, TagSuggestionState state) in rows.Where(row => row.State != TagSuggestionState.Pending))
        {
            unitOfWork.SuggestionRepository.SetState(item.Name, tagName, state);
        }

        unitOfWork.SaveChanges();
    }

    private void AddTag(string name)
    {
        using IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder);
        unitOfWork.AsDbContext().Tags.Add(new TagDao(name));
        unitOfWork.SaveChanges();
    }

    [Fact]
    public async Task Load_ShowsPendingSuggestionsOfTheSelectedImageOnly()
    {
        AddTag("Vacances");
        AddTag("Famille");
        ItemDao first = AddItem("a.jpg");
        ItemDao second = AddItem("b.jpg");
        AddSuggestions(first, ("Vacances", TagSuggestionState.Pending), ("Famille", TagSuggestionState.Rejected));
        AddSuggestions(second, ("Vacances", TagSuggestionState.Pending));

        TagSuggestionsViewModel viewModel = new(_factory);
        await viewModel.LoadAsync(_folder, new CanHaveTag("a.jpg"));

        TagSuggestionItemViewModel item = Assert.Single(viewModel.Items);
        Assert.Equal("Vacances", item.TagName);
        Assert.True(viewModel.HasSuggestions);
    }

    [Fact]
    public async Task Load_HidesSuggestionsWhoseTagWasDeleted()
    {
        ItemDao item = AddItem("a.jpg");
        AddSuggestions(item, ("Disparu", TagSuggestionState.Pending));

        TagSuggestionsViewModel viewModel = new(_factory);
        await viewModel.LoadAsync(_folder, new CanHaveTag("a.jpg"));

        Assert.Empty(viewModel.Items);
        Assert.False(viewModel.HasSuggestions);
    }

    [Fact]
    public async Task Accept_AssignsTheTagAndRecordsTheFeedback()
    {
        AddTag("Famille");
        ItemDao item = AddItem("a.jpg");
        AddSuggestions(item, ("Famille", TagSuggestionState.Pending));

        CanHaveTag image = new("a.jpg");
        TagSuggestionsViewModel viewModel = new(_factory);
        bool changed = false;
        viewModel.TagsChanged += (_, _) => changed = true;

        await viewModel.LoadAsync(_folder, image);
        await viewModel.Items.Single().AcceptCommand.Execute().FirstAsync();

        Assert.Empty(viewModel.Items);
        Assert.True(image.Has(new Tag("Famille")));
        Assert.True(changed);

        using IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder);
        TagSuggestionDao suggestion = Assert.Single(unitOfWork.SuggestionRepository.GetSuggestions("a.jpg"));
        Assert.Equal(TagSuggestionState.Accepted, suggestion.State);
        Assert.Contains(unitOfWork.TagRepository.GetAllTagForHash($"hash-a.jpg"), tag => tag.Name == "Famille");
    }

    [Fact]
    public async Task Reject_RemovesTheSuggestionWithoutAssigningTheTag()
    {
        AddTag("Famille");
        ItemDao item = AddItem("a.jpg");
        AddSuggestions(item, ("Famille", TagSuggestionState.Pending));

        CanHaveTag image = new("a.jpg");
        TagSuggestionsViewModel viewModel = new(_factory);
        bool changed = false;
        viewModel.TagsChanged += (_, _) => changed = true;

        await viewModel.LoadAsync(_folder, image);
        await viewModel.Items.Single().RejectCommand.Execute().FirstAsync();

        Assert.Empty(viewModel.Items);
        Assert.False(image.Has(new Tag("Famille")));
        Assert.False(changed);

        using IUnitOfWork unitOfWork = _factory.GetUnitOfWork(_folder);
        TagSuggestionDao suggestion = Assert.Single(unitOfWork.SuggestionRepository.GetSuggestions("a.jpg"));
        Assert.Equal(TagSuggestionState.Rejected, suggestion.State);
    }

    [Fact]
    public async Task Load_WithoutImage_ClearsTheStrip()
    {
        AddTag("Famille");
        ItemDao item = AddItem("a.jpg");
        AddSuggestions(item, ("Famille", TagSuggestionState.Pending));

        TagSuggestionsViewModel viewModel = new(_factory);
        await viewModel.LoadAsync(_folder, new CanHaveTag("a.jpg"));
        await viewModel.LoadAsync(_folder, null);

        Assert.Empty(viewModel.Items);
        Assert.False(viewModel.HasSuggestions);
    }

    [Fact]
    public async Task Reason_ExplainsTheEvidence()
    {
        AddTag("Famille");
        ItemDao item = AddItem("repas-de-famille.jpg");
        AddSuggestions(item, ("Famille", TagSuggestionState.Pending));

        TagSuggestionsViewModel viewModel = new(_factory);
        await viewModel.LoadAsync(_folder, new CanHaveTag("repas-de-famille.jpg"));

        TagSuggestionItemViewModel suggestion = Assert.Single(viewModel.Items);
        Assert.Contains("repas-de-famille.jpg", suggestion.Reason);
        Assert.Equal(suggestion.Score.ToString("P0", System.Globalization.CultureInfo.CurrentCulture), suggestion.ScoreText);
    }
}

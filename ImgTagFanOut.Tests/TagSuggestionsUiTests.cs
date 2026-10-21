using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.Models.Suggestions;
using ImgTagFanOut.ViewModels;
using ImgTagFanOut.Views;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Tests;

public class TagSuggestionsUiTests : IDisposable
{
    private readonly string _tempDir;

    public TagSuggestionsUiTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ImgTagFanOut.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [AvaloniaFact]
    public async Task SuggestionStrip_ShowsPendingTagsAndAcceptsOne()
    {
        FakeImageGenerator.WritePng(_tempDir, "vacances.png");

        await using (IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(_tempDir))
        {
            await unitOfWork.TagRepository.AddOrUpdateItem(new CanHaveTag("vacances.png"), _ => Task.FromResult("hash-1"));
            unitOfWork.TagRepository.TryCreateTag("Vacances", out _);
            await unitOfWork.SaveChangesAsync();

            ImgTagFanOutDbContext seedDb = unitOfWork.AsDbContext();
            ItemDao item = seedDb.Items.Single();
            unitOfWork.SuggestionRepository.ReplacePending(item.ItemId, item.Hash!, "v1",
                [new TagSuggestionRecord("Vacances", 0.8, TagSuggestionSource.Similar, "3")]);
            await unitOfWork.SaveChangesAsync();
        }

        MainWindowViewModel viewModel = new() { WorkingFolder = _tempDir };
        await viewModel.OpenFolder(_tempDir);
        await viewModel.ScanFolderCommand.Execute(Unit.Default).FirstAsync();

        MainWindow window = new() { DataContext = viewModel };
        window.Show();

        viewModel.SelectedImage = viewModel.FilteredImages.Single();
        for (int i = 0; i < 40 && viewModel.Suggestions.Items.Count == 0; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
        }

        Dispatcher.UIThread.RunJobs();
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        TagSuggestionsView view = window.GetVisualDescendants().OfType<TagSuggestionsView>().Single();
        Assert.Same(viewModel.Suggestions, view.DataContext);

        ItemsControl list = view.GetVisualDescendants().OfType<ItemsControl>().Single();
        Assert.Same(viewModel.Suggestions, list.DataContext);
        Assert.NotNull(list.ItemsSource);
        Assert.Single(list.ItemsSource!.Cast<object>());
        Assert.Equal(1, list.ItemCount);

        TagSuggestionItemViewModel suggestion = viewModel.Suggestions.Items.Single();
        Button? accept = window.FindButtonForCommand(suggestion.AcceptCommand);
        Assert.NotNull(accept);

        await suggestion.AcceptCommand.Execute().FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(viewModel.Suggestions.Items);

        using ImgTagFanOutDbContext db = new(new TagCache(), _tempDir);
        ItemDao stored = db.Items.Single(row => row.Name == "vacances.png");
        Assert.Contains(
            db.ItemTags.Include(link => link.Tag).Where(link => link.ItemForeignKey == stored.ItemId).Select(link => link.Tag.Name),
            name => name == "Vacances");
        Assert.Equal(
            TagSuggestionState.Accepted,
            db.TagSuggestions.Single().State);

        window.Close();
    }
}

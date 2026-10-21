using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.Models.Suggestions;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Tests;

public class TagSuggestionReviewTests : IDisposable
{
    private readonly string _tempDir;

    public TagSuggestionReviewTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ImgTagFanOut.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        FakeImageGenerator.WritePng(_tempDir, "a.png");
        FakeImageGenerator.WritePng(_tempDir, "b.png");
        FakeImageGenerator.WritePng(_tempDir, "c.png");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private async Task SeedAsync()
    {
        await using IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(_tempDir);

        foreach (string name in new[] { "a.png", "b.png", "c.png" })
        {
            await unitOfWork.TagRepository.AddOrUpdateItem(new CanHaveTag(name), _ => Task.FromResult($"hash-{name}"));
        }

        unitOfWork.TagRepository.TryCreateTag("Vacances", out _);
        await unitOfWork.SaveChangesAsync();

        foreach (string name in new[] { "a.png", "c.png" })
        {
            ItemDao item = unitOfWork.AsDbContext().Items.Single(row => row.Name == name);
            unitOfWork.SuggestionRepository.ReplacePending(item.ItemId, item.Hash!, "v1",
                [new TagSuggestionRecord("Vacances", 0.8, TagSuggestionSource.Path, name)]);
        }

        await unitOfWork.SaveChangesAsync();
    }

    [AvaloniaFact]
    public async Task NextSuggestion_SkipsImagesWithoutSuggestionsAndWraps()
    {
        await SeedAsync();

        MainWindowViewModel viewModel = new() { WorkingFolder = _tempDir };
        await viewModel.OpenFolder(_tempDir);
        await viewModel.ScanFolderCommand.Execute(Unit.Default).FirstAsync();

        for (int i = 0; i < 40 && viewModel.FilteredImages.Count < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
        }

        viewModel.SelectedImage = viewModel.FilteredImages.Single(image => image.Item == "b.png");
        await viewModel.NextSuggestionCommand.Execute(Unit.Default).FirstAsync();

        Assert.Equal("c.png", viewModel.SelectedImage?.Item);

        await viewModel.NextSuggestionCommand.Execute(Unit.Default).FirstAsync();

        Assert.Equal("a.png", viewModel.SelectedImage?.Item);
    }
}

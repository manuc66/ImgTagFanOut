using Avalonia.Headless.XUnit;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Tests;

public class MainWindowViewModelTests : IDisposable
{
    private readonly string _tempDir;

    public MainWindowViewModelTests()
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

    private async Task SeedTagAsync(string tagName)
    {
        await using IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(_tempDir);
        unitOfWork.TagRepository.TryCreateTag(tagName, out _);
        await unitOfWork.SaveChangesAsync();
    }

    [AvaloniaFact]
    public async Task OpenFolder_PopulatesTagToImagesTagList()
    {
        await SeedTagAsync("cat");
        await SeedTagAsync("dog");

        MainWindowViewModel vm = new() { WorkingFolder = _tempDir };

        await vm.OpenFolder(_tempDir);

        Assert.Equal(2, vm.TagToImages.TagList.Count);
        Assert.Contains(vm.TagToImages.TagList, t => t.Name == "cat");
        Assert.Contains(vm.TagToImages.TagList, t => t.Name == "dog");
    }
}
using System.Reactive.Linq;
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

    [AvaloniaFact]
    public void HasWorkingFolder_TracksTheSelectedFolder()
    {
        MainWindowViewModel vm = new();
        Assert.False(vm.HasWorkingFolder);

        vm.WorkingFolder = _tempDir;
        Assert.True(vm.HasWorkingFolder);

        vm.WorkingFolder = null;
        Assert.False(vm.HasWorkingFolder);
    }

    [AvaloniaFact]
    public async Task EjectDatabase_RemovesTheDatabaseAndResetsTheWorkspace()
    {
        await SeedTagAsync("cat");

        MainWindowViewModel vm = new() { WorkingFolder = _tempDir };
        await vm.OpenFolder(_tempDir);
        vm.ShowEjectDatabaseDialog.RegisterHandler(context => context.SetOutput(true));
        Assert.True(DbContextFactory.DatabaseExists(_tempDir));

        await vm.EjectDatabaseCommand.Execute().FirstAsync();

        Assert.False(DbContextFactory.DatabaseExists(_tempDir));
        Assert.Null(vm.WorkingFolder);
        Assert.Null(vm.TargetFolder);
        Assert.Empty(vm.TagToImages.TagList);
        Assert.Empty(vm.FilteredImages);
    }

    [AvaloniaFact]
    public async Task EjectDatabase_KeepsTheDatabaseWhenCancelled()
    {
        await SeedTagAsync("cat");

        MainWindowViewModel vm = new() { WorkingFolder = _tempDir };
        await vm.OpenFolder(_tempDir);
        vm.ShowEjectDatabaseDialog.RegisterHandler(context => context.SetOutput(false));

        await vm.EjectDatabaseCommand.Execute().FirstAsync();

        Assert.True(DbContextFactory.DatabaseExists(_tempDir));
        Assert.Equal(_tempDir, vm.WorkingFolder);
    }
}
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Headless.XUnit;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.Models.Faces;
using ImgTagFanOut.ViewModels;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;

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
    public async Task RemovingAPersonLabel_ForgetsTheFacesOfThatPhoto()
    {
        string photo = Path.Combine(_tempDir, "photo.jpg");
        File.WriteAllBytes(photo, [1, 2, 3]);
        string hash = await new HashEvaluator().ComputeHashAsync(photo, CancellationToken.None);

        await SeedTagAsync("Maman");

        using (ImgTagFanOutDbContext db = new(new TagCache(), _tempDir))
        {
            db.Database.Migrate();

            ItemDao item = new("photo.jpg") { Hash = hash };
            db.Items.Add(item);
            db.SaveChanges();

            db.FaceRepository.Replace(
                item,
                hash,
                "det",
                [new AnalyzedFace(new FaceDetection(0.1f, 0.1f, 0.2f, 0.2f, 0.9f, new float[FaceLandmarks.ValueCount]), [1, 2, 3, 4])],
                "emb");
            db.SaveChanges();

            db.FaceRepository.SetPersonTag(db.Faces.Single(), db.Tags.Single(tag => tag.Name == "Maman"));
            db.TagRepository.AddTagToItem(new Tag("Maman"), new CanHaveTag("photo.jpg"));
            db.SaveChanges();
        }

        MainWindowViewModel vm = new() { WorkingFolder = _tempDir };
        await vm.OpenFolder(_tempDir);
        await vm.ScanFolderCommand.Execute(Unit.Default).FirstAsync();

        for (int i = 0; i < 20 && vm.FilteredImages.Count == 0; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
        }

        vm.SelectedImage = vm.FilteredImages.Single();
        Assert.True(vm.SelectedImage.Has(new Tag("Maman")));

        await vm.ToggleAssignTagToImageCommand.Execute(new Tag("Maman")).FirstAsync();

        using (ImgTagFanOutDbContext db = new(new TagCache(), _tempDir))
        {
            Assert.Empty(db.FaceTags);
        }
    }

    [AvaloniaFact]
    public async Task ReloadTags_ShowsATagCreatedBehindTheViewModelsBack()
    {
        MainWindowViewModel vm = new() { WorkingFolder = _tempDir };
        await vm.OpenFolder(_tempDir);
        Assert.Empty(vm.TagToImages.TagList);

        // Ce que fait la bande de visages quand on nomme quelqu'un.
        await SeedTagAsync("Maman");
        vm.ReloadTags();

        Assert.Contains(vm.TagToImages.TagList, tag => tag.Name == "Maman");
        Assert.Contains(vm.FilteredTagList, tag => tag.Tag.Name == "Maman");
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
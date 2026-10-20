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
    public async Task PersonLabels_TellWhenTheyTeachNothing_AndForgetTheFacesWhenRemoved()
    {
        string linked = Path.Combine(_tempDir, "linked.jpg");
        string plain = Path.Combine(_tempDir, "plain.jpg");
        File.WriteAllBytes(linked, [1, 2, 3]);
        File.WriteAllBytes(plain, [4, 5, 6]);

        await SeedTagAsync("Maman");

        using (ImgTagFanOutDbContext db = new(new TagCache(), _tempDir))
        {
            db.Database.Migrate();

            ItemDao item = new("linked.jpg") { Hash = await new HashEvaluator().ComputeHashAsync(linked, CancellationToken.None) };
            db.Items.Add(new ItemDao("plain.jpg") { Hash = await new HashEvaluator().ComputeHashAsync(plain, CancellationToken.None) });
            db.Items.Add(item);
            db.SaveChanges();

            db.FaceRepository.Replace(
                item,
                item.Hash!,
                "det",
                [new AnalyzedFace(new FaceDetection(0.1f, 0.1f, 0.2f, 0.2f, 0.9f, new float[FaceLandmarks.ValueCount]), [1, 2, 3, 4])],
                "emb");
            db.SaveChanges();

            db.FaceRepository.SetPersonTag(db.Faces.Single(), db.Tags.Single(tag => tag.Name == "Maman"));
            db.TagRepository.AddTagToItem(new Tag("Maman"), new CanHaveTag("linked.jpg"));
            db.SaveChanges();
        }

        MainWindowViewModel vm = new() { WorkingFolder = _tempDir };
        await vm.OpenFolder(_tempDir);
        await vm.ScanFolderCommand.Execute(Unit.Default).FirstAsync();

        for (int i = 0; i < 20 && vm.FilteredImages.Count < 2; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
        }

        // Ajouter le libellé sur une photo sans visage associé n'apprend rien : on le dit.
        vm.SelectedImage = vm.FilteredImages.Single(image => image.Item == "plain.jpg");
        await vm.ToggleAssignTagToImageCommand.Execute(new Tag("Maman")).FirstAsync();

        Assert.NotNull(vm.StatusMessage);
        Assert.Contains("Maman", vm.StatusMessage);

        // Le retirer d'une photo où un visage est associé oublie ce visage.
        vm.SelectedImage = vm.FilteredImages.Single(image => image.Item == "linked.jpg");
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
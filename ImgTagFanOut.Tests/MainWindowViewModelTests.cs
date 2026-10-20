using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
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

    [AvaloniaFact]
    public async Task OpenFolder_WithAMalformedShowDoneSetting_FallsBackToFalse()
    {
        await using (IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(_tempDir))
        {
            unitOfWork.ParameterRepository.Update("show_done", "definitely-not-a-boolean");
            await unitOfWork.SaveChangesAsync();
        }

        MainWindowViewModel vm = new() { WorkingFolder = _tempDir };
        await vm.OpenFolder(_tempDir);

        Assert.False(vm.ShowDone);
    }

    [AvaloniaFact]
    public async Task ScanFolder_WhenTheScannerFails_ReportsTheErrorAndLeavesTheWindowUsable()
    {
        MainWindowViewModel vm = new(
            new UnitOfWorkFactory(),
            new ThrowingFolderScan(new IOException("the drive disappeared")),
            new HashEvaluator(),
            new NullThumbnailProvider(),
            new FakeFileManagerHandler());
        vm.WorkingFolder = _tempDir;
        await vm.OpenFolder(_tempDir);

        await vm.ScanFolderCommand.Execute(Unit.Default).FirstAsync();

        Assert.False(vm.IsBusy);
        Assert.NotNull(vm.StatusMessage);
        Assert.Contains("the drive disappeared", vm.StatusMessage);
        Assert.Contains(ImgTagFanOut.Resources.Resources.ScanFailed, vm.StatusMessage);
    }

    [AvaloniaFact]
    public async Task AnalyzeFaces_WhenTheScanFails_ReportsTheErrorAndClearsTheBusyState()
    {
        MainWindowViewModel vm = new(
            new UnitOfWorkFactory(),
            new FolderScan(),
            new HashEvaluator(),
            new NullThumbnailProvider(),
            new FakeFileManagerHandler(),
            new ThrowingFaceScan(new InvalidOperationException("broken model")));
        vm.WorkingFolder = _tempDir;
        await vm.OpenFolder(_tempDir);

        await vm.AnalyzeFacesCommand.Execute(Unit.Default).FirstAsync();

        Assert.False(vm.IsBusy);
        Assert.Null(vm.BusyMessage);
        Assert.NotNull(vm.StatusMessage);
        Assert.Contains("broken model", vm.StatusMessage);
        Assert.Contains(ImgTagFanOut.Resources.Resources.FaceScanFailed, vm.StatusMessage);
    }

    private void WriteImage(string name)
    {
        File.WriteAllBytes(Path.Combine(_tempDir, name), [1, 2, 3]);
    }

    private async Task ScanAndWaitAsync(MainWindowViewModel vm, int expectedCount)
    {
        await vm.ScanFolderCommand.Execute(Unit.Default).FirstAsync();

        for (int i = 0; i < 100 && vm.FilteredImages.Count < expectedCount; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal(expectedCount, vm.FilteredImages.Count);
    }

    private static async Task WaitForTagsAsync(MainWindowViewModel vm, int expectedCount)
    {
        for (int i = 0; i < 100 && vm.FilteredTagList.Count < expectedCount; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal(expectedCount, vm.FilteredTagList.Count);
    }

    [AvaloniaFact]
    public async Task DoneCommand_MarksTheSelectedImageDoneInTheDatabase()
    {
        WriteImage("a.jpg");
        MainWindowViewModel vm = new() { WorkingFolder = _tempDir };
        await vm.OpenFolder(_tempDir);
        await ScanAndWaitAsync(vm, 1);
        vm.SelectedImage = vm.FilteredImages.Single();

        await vm.DoneCommand.Execute(Unit.Default).FirstAsync();

        Assert.True(vm.SelectedImage!.Done);

        await using IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(_tempDir);
        Assert.True(unitOfWork.TagRepository.GetAllItems().Single().Done);
    }

    [AvaloniaFact]
    public async Task AddAndDeleteTag_UpdateTheListAndTheDatabase()
    {
        MainWindowViewModel vm = new() { WorkingFolder = _tempDir };
        await vm.OpenFolder(_tempDir);

        vm.TagFilterInput = "  cat  ";
        await vm.AddToTagListCommand.Execute(Unit.Default).FirstAsync();
        await WaitForTagsAsync(vm, 1);

        Assert.Equal("cat", vm.FilteredTagList.Single().Tag.Name);

        await vm.DeleteTagCommand.Execute(vm.FilteredTagList.Single()).FirstAsync();

        Assert.Empty(vm.FilteredTagList);

        await using IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(_tempDir);
        Assert.Empty(unitOfWork.TagRepository.GetAllTag());
    }

    [AvaloniaFact]
    public async Task AllAndNoneCommands_AssignThenRemoveEveryLabelOnTheImage()
    {
        WriteImage("a.jpg");
        await SeedTagAsync("cat");
        await SeedTagAsync("dog");

        MainWindowViewModel vm = new() { WorkingFolder = _tempDir };
        await vm.OpenFolder(_tempDir);
        await ScanAndWaitAsync(vm, 1);
        await WaitForTagsAsync(vm, 2);
        vm.SelectedImage = vm.FilteredImages.Single();

        await vm.AllCommand.Execute(Unit.Default).FirstAsync();
        Assert.Equal(2, vm.SelectedImage!.Tags.Count);

        await vm.NoneCommand.Execute(Unit.Default).FirstAsync();
        Assert.Empty(vm.SelectedImage.Tags);

        await using IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(_tempDir);
        Assert.Empty(unitOfWork.AsDbContext().ItemTags);
    }

    [AvaloniaFact]
    public async Task OpenLocateAndOpenTarget_UseTheFileManagerOnlyWhenThePathsExist()
    {
        WriteImage("a.jpg");
        FakeFileManagerHandler fileManager = new();
        MainWindowViewModel vm = new(
            new UnitOfWorkFactory(),
            new FolderScan(),
            new HashEvaluator(),
            new NullThumbnailProvider(),
            fileManager);
        vm.WorkingFolder = _tempDir;
        await vm.OpenFolder(_tempDir);
        await ScanAndWaitAsync(vm, 1);
        vm.SelectedImage = vm.FilteredImages.Single();

        await vm.OpenCommand.Execute(Unit.Default).FirstAsync();
        await vm.LocateCommand.Execute(Unit.Default).FirstAsync();
        Assert.Equal(Path.Combine(_tempDir, "a.jpg"), fileManager.LastOpenedFile);
        Assert.Equal(Path.Combine(_tempDir, "a.jpg"), fileManager.LastRevealedFile);

        vm.TargetFolder = _tempDir;
        await vm.OpenTargetFolderCommand.Execute(Unit.Default).FirstAsync();
        Assert.Equal(_tempDir, fileManager.LastOpenedFolder);
    }

    private sealed class ThrowingFolderScan : IFolderScan
    {
        private readonly Exception _exception;

        public ThrowingFolderScan(Exception exception)
        {
            _exception = exception;
        }

        public Task ScanFolder(CancellationToken cancellationToken, string workingFolder, DynamicData.SourceList<CanHaveTag> images)
        {
            return Task.FromException(_exception);
        }
    }

    private sealed class ThrowingFaceScan : IFaceScan
    {
        private readonly Exception _exception;

        public ThrowingFaceScan(Exception exception)
        {
            _exception = exception;
        }

        public Task<FaceScanSummary> ScanAsync(string workingFolder, IProgress<FaceScanProgress>? progress, CancellationToken cancellationToken)
        {
            return Task.FromException<FaceScanSummary>(_exception);
        }
    }

    private sealed class NullThumbnailProvider : IThumbnailProvider
    {
        public Task<Bitmap?> GetThumbnail(string fullFilePath) => Task.FromResult<Bitmap?>(null);

        public Task<Bitmap?> GetThumbnail(string fullFilePath, uint targetWidth) => Task.FromResult<Bitmap?>(null);
    }
}
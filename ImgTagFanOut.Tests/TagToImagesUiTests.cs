using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.ViewModels;
using ImgTagFanOut.Views;

namespace ImgTagFanOut.Tests;

public class TagToImagesUiTests : IDisposable
{
    private readonly string _tempDir;

    public TagToImagesUiTests()
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

    private async Task SeedDataAsync()
    {
        FakeImageGenerator.WritePng(_tempDir, "cat1.png");
        FakeImageGenerator.WritePng(_tempDir, "cat2.png");
        FakeImageGenerator.WritePng(_tempDir, "dog1.png");

        await using IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(_tempDir);
        await unitOfWork.TagRepository.AddOrUpdateItem(new CanHaveTag("cat1.png"), _ => Task.FromResult("h1"));
        await unitOfWork.TagRepository.AddOrUpdateItem(new CanHaveTag("cat2.png"), _ => Task.FromResult("h2"));
        await unitOfWork.TagRepository.AddOrUpdateItem(new CanHaveTag("dog1.png"), _ => Task.FromResult("h3"));
        await unitOfWork.SaveChangesAsync();

        unitOfWork.TagRepository.TryCreateTag("cat", out Tag? cat);
        unitOfWork.TagRepository.TryCreateTag("dog", out Tag? dog);
        await unitOfWork.SaveChangesAsync();

        unitOfWork.TagRepository.AddTagToItem(cat!, new CanHaveTag("cat1.png"));
        unitOfWork.TagRepository.AddTagToItem(cat!, new CanHaveTag("cat2.png"));
        unitOfWork.TagRepository.AddTagToItem(dog!, new CanHaveTag("dog1.png"));
        await unitOfWork.SaveChangesAsync();
    }

    [AvaloniaFact]
    public async Task ClickingTagsTab_AndSelectingTag_ShowsImagesInUi()
    {
        await SeedDataAsync();
        MainWindowViewModel vm = new() { WorkingFolder = _tempDir };
        await vm.OpenFolder(_tempDir);

        MainWindow window = new() { DataContext = vm };
        window.Show();

        // Simulate clicking the "Tags" tab
        TabControl tabControl = window.FindControl<TabControl>("tabControl");
        Assert.NotNull(tabControl);
        TabItem? tagsTab = tabControl.Items.OfType<TabItem>().FirstOrDefault(t => (string?)t.Header == "Libellés");
        Assert.NotNull(tagsTab);
        tabControl.SelectedItem = tagsTab;
        Dispatcher.UIThread.RunJobs();

        var tagListBox = window.FindByAutomationId<ListBox>(AutomationIds.TagToImagesTagList);
        Assert.NotNull(tagListBox);
        Assert.Equal(2, tagListBox.ItemCount);

        // Simulate clicking the "cat" tag -> images list should show 2 images
        tagListBox!.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();

        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        var imageListBox = window.FindByAutomationId<ListBox>(AutomationIds.TagToImagesImageList);
        Assert.NotNull(imageListBox);
        Assert.Equal(2, imageListBox.ItemCount);

        window.Close();
    }
}
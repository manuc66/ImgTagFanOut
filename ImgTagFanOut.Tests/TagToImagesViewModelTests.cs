using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.ViewModels;
using ReactiveUI;

namespace ImgTagFanOut.Tests;

public class TagToImagesViewModelTests : IDisposable
{
    private readonly string _tempDir;

    public TagToImagesViewModelTests()
    {
        RxApp.MainThreadScheduler = System.Reactive.Concurrency.CurrentThreadScheduler.Instance;
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

    private async Task<(TagToImagesViewModel vm, List<string> items)> SetupAsync(
        Dictionary<string, string[]> tagAssignments)
    {
        List<string> items = new();
        foreach ((string tag, string[] files) in tagAssignments)
        {
            foreach (string file in files)
            {
                string path = FakeImageGenerator.WritePng(_tempDir, file);
                items.Add(Path.GetRelativePath(_tempDir, path));
            }
        }

        await using IUnitOfWork unitOfWork = await DbContextFactory.GetUnitOfWorkAsync(_tempDir);
        foreach (string item in items)
        {
            await unitOfWork.TagRepository.AddOrUpdateItem(new CanHaveTag(item), _ => Task.FromResult("hash"));
        }

        await unitOfWork.SaveChangesAsync();

        foreach ((string tag, string[] files) in tagAssignments)
        {
            unitOfWork.TagRepository.TryCreateTag(tag, out Tag? createdTag);
        }

        await unitOfWork.SaveChangesAsync();

        foreach ((string tag, string[] files) in tagAssignments)
        {
            Tag? createdTag = unitOfWork.TagRepository.GetAllTag().First(t => t.Name == tag);
            foreach (string file in files)
            {
                unitOfWork.TagRepository.AddTagToItem(createdTag!, new CanHaveTag(file));
            }
        }

        await unitOfWork.SaveChangesAsync();

        TagToImagesViewModel vm = new(() => _tempDir);
        return (vm, items);
    }

    [Fact]
    public async Task LoadTags_LoadsAllTagsFromDatabase()
    {
        (TagToImagesViewModel vm, _) = await SetupAsync(new Dictionary<string, string[]>
        {
            ["cat"] = new[] { "cat1.png", "cat2.png" },
            ["dog"] = new[] { "dog1.png" },
        });

        vm.LoadTags();

        Assert.Equal(2, vm.TagList.Count);
        Assert.Contains(vm.TagList, t => t.Name == "cat");
        Assert.Contains(vm.TagList, t => t.Name == "dog");
    }

    [Fact]
    public async Task SelectingTag_ShowsOnlyImagesWithThatTag()
    {
        (TagToImagesViewModel vm, _) = await SetupAsync(new Dictionary<string, string[]>
        {
            ["cat"] = new[] { "cat1.png", "cat2.png" },
            ["dog"] = new[] { "dog1.png" },
        });
        vm.LoadTags();

        vm.SelectedTag = vm.TagList.First(t => t.Name == "cat");
        await WaitUntilAsync(() => vm.FilteredImages.Count == 2);

        Assert.Equal(2, vm.FilteredImages.Count);
        Assert.All(vm.FilteredImages, i => Assert.Contains("cat", i.Item));
    }

    [Fact]
    public async Task SelectingTag_ThenSwitchingTag_ReplacesImages()
    {
        (TagToImagesViewModel vm, _) = await SetupAsync(new Dictionary<string, string[]>
        {
            ["cat"] = new[] { "cat1.png" },
            ["dog"] = new[] { "dog1.png", "dog2.png" },
        });
        vm.LoadTags();

        vm.SelectedTag = vm.TagList.First(t => t.Name == "cat");
        await WaitUntilAsync(() => vm.FilteredImages.Count == 1);

        vm.SelectedTag = vm.TagList.First(t => t.Name == "dog");
        await WaitUntilAsync(() => vm.FilteredImages.Count == 2);

        Assert.Equal(2, vm.FilteredImages.Count);
        Assert.All(vm.FilteredImages, i => Assert.Contains("dog", i.Item));
    }

    [Fact]
    public async Task SelectingTag_WithNoImages_ShowsEmptyList()
    {
        (TagToImagesViewModel vm, _) = await SetupAsync(new Dictionary<string, string[]>
        {
            ["cat"] = new[] { "cat1.png" },
        });
        vm.LoadTags();

        vm.SelectedTag = vm.TagList.First(t => t.Name == "cat");
        await WaitUntilAsync(() => vm.FilteredImages.Count == 1);

        Tag? unusedTag = new("unused");
        vm.SelectedTag = unusedTag;
        await Task.Delay(200);

        Assert.Empty(vm.FilteredImages);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        long start = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - start > timeoutMs)
            {
                throw new TimeoutException("Condition not reached in time");
            }

            await Task.Delay(20);
        }
    }
}
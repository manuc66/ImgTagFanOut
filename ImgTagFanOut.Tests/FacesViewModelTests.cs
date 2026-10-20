using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.Models.Faces;
using ImgTagFanOut.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Tests;

public class FacesViewModelTests : IDisposable
{
    private readonly string _folder;
    private readonly SqliteConnection _connection;
    private readonly TestUnitOfWorkFactory _unitOfWorkFactory;
    private readonly FacesViewModel _viewModel;

    public FacesViewModelTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "ImgTagFanOut-Faces-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _unitOfWorkFactory = new TestUnitOfWorkFactory(_connection);

        using (IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(_folder))
        {
            ItemDao item = new("photo.jpg");
            unitOfWork.AsDbContext().Items.Add(item);
            unitOfWork.SaveChanges();

            unitOfWork.AsDbContext().Tags.Add(new TagDao("Maman"));
            unitOfWork.SaveChanges();

            unitOfWork.FaceRepository.Replace(
                item,
                "hash-1",
                "det-v1",
                [
                    Face(0.1f),
                    Face(0.6f),
                ],
                "emb-v1");
            unitOfWork.SaveChanges();
        }

        _viewModel = new FacesViewModel(_unitOfWorkFactory, new FakeThumbnailProvider());
    }

    public void Dispose()
    {
        _connection.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private static AnalyzedFace Face(float x)
    {
        return new AnalyzedFace(
            new FaceDetection(x, 0.1f, 0.2f, 0.2f, 0.9f, new float[FaceLandmarks.ValueCount]),
            [1, 2, 3, 4]);
    }

    private CanHaveTag Item()
    {
        return new CanHaveTag("photo.jpg");
    }

    [Fact]
    public async Task Load_ShowsTheFacesAndTheKnownPersons()
    {
        using (IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(_folder))
        {
            FaceDao face = unitOfWork.FaceRepository.GetFacesWithPerson("photo.jpg")[0].Face;
            unitOfWork.FaceRepository.SetPersonTag(face, unitOfWork.TagRepository.GetTagDao("Maman"));
            unitOfWork.SaveChanges();
        }

        await _viewModel.LoadAsync(_folder, Item());

        Assert.True(_viewModel.HasFaces);
        Assert.Equal(2, _viewModel.Faces.Count);
        Assert.Equal("Maman", _viewModel.Faces[0].Person);
        Assert.Null(_viewModel.Faces[1].Person);
        Assert.Equal("Maman", Assert.Single(_viewModel.PersonTags).Name);
    }

    [Fact]
    public async Task Assign_NamesASecondFaceWithAnExistingPerson()
    {
        CanHaveTag item = Item();
        await _viewModel.LoadAsync(_folder, item);

        _viewModel.SelectedFace = _viewModel.Faces[0];
        _viewModel.NewPersonName = "Maman";
        await _viewModel.CreatePersonCommand.Execute().FirstAsync();

        _viewModel.SelectedFace = _viewModel.Faces[1];
        _viewModel.SelectedPersonTag = _viewModel.PersonTags.Single(tag => tag.Name == "Maman");
        await _viewModel.AssignPersonCommand.Execute().FirstAsync();

        Assert.Equal("Maman", _viewModel.Faces[1].Person);
        Assert.True(item.Has(new Tag("Maman")));

        using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(_folder);
        Assert.Equal(
            2,
            unitOfWork.FaceRepository.GetFacesWithPerson("photo.jpg").Count(row => row.PersonName == "Maman"));
    }

    [Fact]
    public async Task Create_CreatesTheTagAndLinksTheFace()
    {
        CanHaveTag item = Item();
        await _viewModel.LoadAsync(_folder, item);

        _viewModel.SelectedFace = _viewModel.Faces[0];
        _viewModel.NewPersonName = "Papa";

        await _viewModel.CreatePersonCommand.Execute().FirstAsync();

        Assert.Equal("Papa", _viewModel.Faces[0].Person);
        Assert.True(item.Has(new Tag("Papa")));
        Assert.Contains(_viewModel.PersonTags, tag => tag.Name == "Papa");

        using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(_folder);
        Assert.NotNull(unitOfWork.TagRepository.GetTagDao("Papa"));
    }

    [Fact]
    public async Task Forget_RemovesTheFaceLink()
    {
        CanHaveTag item = Item();
        await _viewModel.LoadAsync(_folder, item);

        _viewModel.SelectedFace = _viewModel.Faces[0];
        _viewModel.NewPersonName = "Maman";
        await _viewModel.CreatePersonCommand.Execute().FirstAsync();

        await _viewModel.ClearPersonCommand.Execute().FirstAsync();

        Assert.Null(_viewModel.Faces[0].Person);

        using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(_folder);
        Assert.All(unitOfWork.FaceRepository.GetFacesWithPerson("photo.jpg"), row => Assert.Null(row.PersonName));
    }

    private sealed class FakeThumbnailProvider : IThumbnailProvider
    {
        public Task<Bitmap?> GetThumbnail(string fullFilePath) => Task.FromResult<Bitmap?>(null);

        public Task<Bitmap?> GetThumbnail(string fullFilePath, uint targetWidth) => Task.FromResult<Bitmap?>(null);
    }

    private sealed class TestUnitOfWorkFactory : IUnitOfWorkFactory
    {
        private readonly SqliteConnection _connection;

        public TestUnitOfWorkFactory(SqliteConnection connection)
        {
            _connection = connection;
        }

        public Task<IUnitOfWork> GetUnitOfWorkAsync(string path, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IUnitOfWork>(Create());
        }

        public IUnitOfWork GetUnitOfWork(string path)
        {
            return Create();
        }

        public void ClearTagCache()
        {
        }

        public bool DatabaseExists(string path) => true;

        public void DeleteDatabase(string path)
        {
        }

        private ImgTagFanOutDbContext Create()
        {
            DbContextOptions<ImgTagFanOutDbContext> options = new DbContextOptionsBuilder<ImgTagFanOutDbContext>()
                .UseSqlite(_connection)
                .Options;
            ImgTagFanOutDbContext db = new(options, new TagCache());
            db.Database.EnsureCreated();
            return db;
        }
    }
}

internal static class UnitOfWorkTestExtensions
{
    /// <summary>Context access for tests that need to seed entities.</summary>
    public static ImgTagFanOutDbContext AsDbContext(this IUnitOfWork unitOfWork)
    {
        return (ImgTagFanOutDbContext)unitOfWork;
    }
}

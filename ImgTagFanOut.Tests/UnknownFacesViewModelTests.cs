using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.Models.Faces;
using ImgTagFanOut.ViewModels;
using Microsoft.Data.Sqlite;

namespace ImgTagFanOut.Tests;

public class UnknownFacesViewModelTests : IDisposable
{
    private const string Folder = "/album";

    private readonly SqliteConnection _connection;
    private readonly InMemoryUnitOfWorkFactory _unitOfWorkFactory;
    private readonly UnknownFacesViewModel _viewModel;

    public UnknownFacesViewModelTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _unitOfWorkFactory = new InMemoryUnitOfWorkFactory(_connection);

        using (IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(Folder))
        {
            AddItem(unitOfWork, "one.jpg", [1, 0, 0], [1, 0, 0]);
            AddItem(unitOfWork, "two.jpg", [0, 1, 0], [0, 1, 0]);
            unitOfWork.SaveChanges();
        }

        _viewModel = new UnknownFacesViewModel(() => Folder, _unitOfWorkFactory, new FakeThumbnailProvider());
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private static void AddItem(IUnitOfWork unitOfWork, string name, params float[][] embeddings)
    {
        ImgTagFanOutDbContext db = (ImgTagFanOutDbContext)unitOfWork;
        ItemDao item = new(name) { Hash = "hash-" + name };
        db.Items.Add(item);
        db.SaveChanges();

        unitOfWork.FaceRepository.Replace(
            item,
            item.Hash!,
            "det",
            embeddings
                .Select(embedding => new AnalyzedFace(
                    new FaceDetection(0.1f, 0.1f, 0.2f, 0.2f, 0.9f, new float[FaceLandmarks.ValueCount]),
                    Bytes(embedding)))
                .ToList(),
            "emb");
        db.SaveChanges();
    }

    private static byte[] Bytes(float[] values)
    {
        byte[] bytes = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    [Fact]
    public async Task Refresh_GroupsLookAlikeFaces()
    {
        await _viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(2, _viewModel.Clusters.Count);
        Assert.All(_viewModel.Clusters, cluster => Assert.Equal(2, cluster.Count));
        Assert.False(_viewModel.IsEmpty);
    }

    [Fact]
    public async Task NamingACluster_LinksEveryFaceAndTagsEveryPhoto()
    {
        await _viewModel.RefreshAsync(CancellationToken.None);
        UnknownClusterViewModel cluster = _viewModel.Clusters[0];
        cluster.PersonName = "Maman";

        await _viewModel.NameClusterCommand.Execute(cluster).FirstAsync();

        Assert.Single(_viewModel.Clusters);

        using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(Folder);
        List<FaceWithPerson> named = unitOfWork.FaceRepository.GetFacesWithPerson("one.jpg")
            .Concat(unitOfWork.FaceRepository.GetFacesWithPerson("two.jpg"))
            .Where(row => row.PersonName == "Maman")
            .ToList();

        Assert.Equal(2, named.Count);
        Assert.Contains(unitOfWork.FaceRepository.GetPersonTags(), tag => tag.Name == "Maman");
    }

    [Fact]
    public async Task NamingACluster_WithoutAName_DoesNothing()
    {
        await _viewModel.RefreshAsync(CancellationToken.None);
        UnknownClusterViewModel cluster = _viewModel.Clusters[0];

        await _viewModel.NameClusterCommand.Execute(cluster).FirstAsync();

        Assert.Equal(2, _viewModel.Clusters.Count);
    }

    private sealed class FakeThumbnailProvider : IThumbnailProvider
    {
        public Task<Bitmap?> GetThumbnail(string fullFilePath) => Task.FromResult<Bitmap?>(null);

        public Task<Bitmap?> GetThumbnail(string fullFilePath, uint targetWidth) => Task.FromResult<Bitmap?>(null);
    }
}

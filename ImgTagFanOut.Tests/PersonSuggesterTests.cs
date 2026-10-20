using System.Buffers.Binary;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.Models.Faces;
using ImgTagFanOut.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Tests;

public class PersonSuggesterTests : IDisposable
{
    private const string Folder = "/album";

    private readonly SqliteConnection _connection;
    private readonly InMemoryUnitOfWorkFactory _unitOfWorkFactory;
    private readonly PersonSuggester _suggester;

    public PersonSuggesterTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _unitOfWorkFactory = new InMemoryUnitOfWorkFactory(_connection);
        _suggester = new PersonSuggester(_unitOfWorkFactory);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private void Seed(params (string Person, float[] Embedding)[] namedFaces)
    {
        using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(Folder);

        foreach (string person in namedFaces.Select(face => face.Person).Distinct())
        {
            unitOfWork.TagRepository.TryCreateTag(person, out _);
        }

        unitOfWork.SaveChanges();

        ItemDao item = new("refs.jpg");
        unitOfWork.AsDbContext().Items.Add(item);
        unitOfWork.SaveChanges();

        unitOfWork.FaceRepository.Replace(
            item,
            "hash",
            "det",
            namedFaces
                .Select(face => new AnalyzedFace(
                    new FaceDetection(0, 0, 0.1f, 0.1f, 0.9f, new float[FaceLandmarks.ValueCount]),
                    Embedding(face.Embedding)))
                .ToList(),
            "emb");

        unitOfWork.SaveChanges();

        List<FaceDao> faces = unitOfWork.FaceRepository.GetFaces(item.ItemId).ToList();
        for (int i = 0; i < namedFaces.Length; i++)
        {
            unitOfWork.FaceRepository.SetPersonTag(faces[i], unitOfWork.TagRepository.GetTagDao(namedFaces[i].Person));
        }

        unitOfWork.SaveChanges();
        _suggester.Invalidate(Folder);
    }

    private static byte[] Embedding(float[] values)
    {
        double norm = Math.Sqrt(values.Sum(v => (double)v * v));
        byte[] bytes = new byte[values.Length * sizeof(float)];
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float)), (float)(values[i] / norm));
        }

        return bytes;
    }

    [Fact]
    public void Suggest_ReturnsTheClosestPerson()
    {
        Seed(("Maman", [1f, 0f, 0f]), ("Papa", [0f, 1f, 0f]));

        IReadOnlyList<PersonMatch> matches = _suggester.Suggest(Folder, Embedding([0.99f, 0.1f, 0f]));

        PersonMatch best = Assert.Single(matches);
        Assert.Equal("Maman", best.PersonName);
        Assert.True(best.Score > 0.9f, $"unexpected score: {best.Score}");
    }

    [Fact]
    public void Suggest_ReturnsNothingWhenNoOneIsCloseEnough()
    {
        Seed(("Maman", [1f, 0f, 0f]), ("Papa", [0f, 1f, 0f]));

        Assert.Empty(_suggester.Suggest(Folder, Embedding([0f, 0f, 1f])));
    }

    [Fact]
    public void Suggest_KeepsTheBestExampleRatherThanAnAverage()
    {
        // Two very different photos of the same person: their average matches
        // neither, the best one still has to win.
        Seed(("Maman", [1f, 0f, 0f]), ("Maman", [-1f, 0f, 0f]));

        PersonMatch best = Assert.Single(_suggester.Suggest(Folder, Embedding([1f, 0f, 0f])));

        Assert.Equal("Maman", best.PersonName);
        Assert.True(best.Score > 0.99f);
    }

    [Fact]
    public void Invalidate_SeesTheFacesNamedSinceTheLastCall()
    {
        Seed(("Maman", [1f, 0f, 0f]));
        Assert.Empty(_suggester.Suggest(Folder, Embedding([0f, 1f, 0f])));

        // A new face named "Papa" afterwards.
        using (IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(Folder))
        {
            ItemDao item = unitOfWork.AsDbContext().Items.First(x => x.Name == "refs.jpg");
            unitOfWork.FaceRepository.Replace(
                item,
                "hash-2",
                "det",
                [
                    new AnalyzedFace(
                        new FaceDetection(0, 0, 0.1f, 0.1f, 0.9f, new float[FaceLandmarks.ValueCount]),
                        Embedding([1f, 0f, 0f])),
                    new AnalyzedFace(
                        new FaceDetection(0.5f, 0, 0.1f, 0.1f, 0.9f, new float[FaceLandmarks.ValueCount]),
                        Embedding([0f, 1f, 0f])),
                ],
                "emb");
            unitOfWork.SaveChanges();

            unitOfWork.TagRepository.TryCreateTag("Papa", out _);
            unitOfWork.SaveChanges();

            List<FaceDao> faces = unitOfWork.FaceRepository.GetFaces(item.ItemId).ToList();
            unitOfWork.FaceRepository.SetPersonTag(faces[0], unitOfWork.TagRepository.GetTagDao("Maman"));
            unitOfWork.FaceRepository.SetPersonTag(faces[1], unitOfWork.TagRepository.GetTagDao("Papa"));
            unitOfWork.SaveChanges();
        }

        _suggester.Invalidate(Folder);

        PersonMatch best = Assert.Single(_suggester.Suggest(Folder, Embedding([0f, 1f, 0f])));
        Assert.Equal("Papa", best.PersonName);
    }
}

/// <summary>UnitOfWorkFactory over one shared in-memory SQLite connection.</summary>
internal sealed class InMemoryUnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly SqliteConnection _connection;

    public InMemoryUnitOfWorkFactory(SqliteConnection connection)
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

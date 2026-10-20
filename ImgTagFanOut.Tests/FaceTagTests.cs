using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.Models.Faces;
using ImgTagFanOut.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Tests;

public class FaceTagTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ImgTagFanOutDbContext _db;
    private readonly FaceRepository _faces;
    private readonly TagRepository _tags;

    public FaceTagTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        DbContextOptions<ImgTagFanOutDbContext> options = new DbContextOptionsBuilder<ImgTagFanOutDbContext>()
            .UseSqlite(_connection)
            .Options;

        TagCache tagCache = new();
        _db = new ImgTagFanOutDbContext(options, tagCache);
        _db.Database.EnsureCreated();
        _faces = new FaceRepository(_db);
        _tags = new TagRepository(_db, tagCache);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private ItemDao CreateItemWithFaces(string name = "photo.jpg", int faceCount = 2)
    {
        ItemDao item = new(name);
        _db.Items.Add(item);
        _db.SaveChanges();

        AnalyzedFace[] faces = Enumerable.Range(0, faceCount)
            .Select(i => new AnalyzedFace(
                new FaceDetection(0.1f * i, 0.1f, 0.2f, 0.2f, 0.9f, new float[FaceLandmarks.ValueCount]),
                [1, 2, 3, 4]))
            .ToArray();

        _faces.Replace(item, "hash-1", "det-v1", faces, "emb-v1");
        _db.SaveChanges();
        return item;
    }

    private TagDao CreateTag(string name)
    {
        TagDao tag = new(name);
        _db.Tags.Add(tag);
        _db.SaveChanges();
        return tag;
    }

    private List<FaceDao> FacesOf(ItemDao item)
    {
        return _db.Faces.Where(x => x.ItemForeignKey == item.ItemId).OrderBy(x => x.Position).ToList();
    }

    [Fact]
    public void SetPersonTag_NamesTheFace()
    {
        ItemDao item = CreateItemWithFaces();
        TagDao maman = CreateTag("Maman");
        FaceDao face = FacesOf(item)[0];

        _faces.SetPersonTag(face, maman);
        _db.SaveChanges();

        Assert.Equal("Maman", _faces.GetPersonTag(face.FaceId)?.Name);
        Assert.Equal("Maman", Assert.Single(_faces.GetPersonTags()).Name);
    }

    [Fact]
    public void SetPersonTag_ReplacesThePreviousName()
    {
        ItemDao item = CreateItemWithFaces();
        FaceDao face = FacesOf(item)[0];
        TagDao maman = CreateTag("Maman");
        TagDao papa = CreateTag("Papa");

        _faces.SetPersonTag(face, maman);
        _db.SaveChanges();
        _faces.SetPersonTag(face, papa);
        _db.SaveChanges();

        Assert.Equal("Papa", _faces.GetPersonTag(face.FaceId)?.Name);
        Assert.Single(_db.FaceTags);
    }

    [Fact]
    public void SetPersonTag_WithNull_ClearsTheName()
    {
        ItemDao item = CreateItemWithFaces();
        FaceDao face = FacesOf(item)[0];
        TagDao maman = CreateTag("Maman");

        _faces.SetPersonTag(face, maman);
        _db.SaveChanges();
        _faces.SetPersonTag(face, null);
        _db.SaveChanges();

        Assert.Null(_faces.GetPersonTag(face.FaceId));
        Assert.Empty(_faces.GetPersonTags());
    }

    [Fact]
    public void SamePersonOnTwoFaces_IsListedOnce()
    {
        ItemDao item = CreateItemWithFaces();
        TagDao maman = CreateTag("Maman");
        List<FaceDao> faces = FacesOf(item);

        _faces.SetPersonTag(faces[0], maman);
        _faces.SetPersonTag(faces[1], maman);
        _db.SaveChanges();

        Assert.Equal(2, _db.FaceTags.Count());
        Assert.Single(_faces.GetPersonTags());
    }

    [Fact]
    public void ReanalyzingTheImage_DropsTheLinksOfVanishedFaces()
    {
        ItemDao item = CreateItemWithFaces(faceCount: 2);
        TagDao maman = CreateTag("Maman");
        _faces.SetPersonTag(FacesOf(item)[0], maman);
        _db.SaveChanges();

        // The file changed: only one face is found again.
        _faces.Replace(
            item,
            "hash-2",
            "det-v1",
            [
                new AnalyzedFace(
                    new FaceDetection(0.1f, 0.1f, 0.2f, 0.2f, 0.9f, new float[FaceLandmarks.ValueCount]),
                    [1, 2, 3, 4]),
            ],
            "emb-v1");
        _db.SaveChanges();

        Assert.Empty(_db.FaceTags);
        Assert.Empty(_faces.GetPersonTags());
    }

    [Fact]
    public void GetPersonFaceCounts_CountsTheFacesLearnedPerPerson()
    {
        ItemDao photo = CreateItemWithFaces(faceCount: 2);
        ItemDao other = CreateItemWithFaces("other.jpg", faceCount: 1);
        TagDao maman = CreateTag("Maman");
        TagDao papa = CreateTag("Papa");

        _faces.SetPersonTag(FacesOf(photo)[0], maman);
        _faces.SetPersonTag(FacesOf(photo)[1], maman);
        _faces.SetPersonTag(FacesOf(other)[0], papa);
        _db.SaveChanges();

        IReadOnlyDictionary<string, int> counts = _faces.GetPersonFaceCounts();

        Assert.Equal(2, counts["Maman"]);
        Assert.Equal(1, counts["Papa"]);
        Assert.Equal(2, counts.Count);
    }

    [Fact]
    public void ClearPersonLinks_OnlyRemovesThatPersonInThatImage()
    {
        ItemDao photo = CreateItemWithFaces(faceCount: 2);
        ItemDao other = CreateItemWithFaces("other.jpg", faceCount: 1);
        TagDao maman = CreateTag("Maman");
        TagDao papa = CreateTag("Papa");

        List<FaceDao> photoFaces = FacesOf(photo);
        FaceDao otherFace = FacesOf(other)[0];
        _faces.SetPersonTag(photoFaces[0], maman);
        _faces.SetPersonTag(photoFaces[1], papa);
        _faces.SetPersonTag(otherFace, maman);
        _db.SaveChanges();

        _faces.ClearPersonLinks(photo.Name, "Maman");
        _db.SaveChanges();

        Assert.Null(_faces.GetPersonTag(photoFaces[0].FaceId));
        Assert.Equal("Papa", _faces.GetPersonTag(photoFaces[1].FaceId)?.Name);
        Assert.Equal("Maman", _faces.GetPersonTag(otherFace.FaceId)?.Name);
    }

    [Fact]
    public void DeletingTheTag_RemovesItsFaceLinks()
    {
        ItemDao item = CreateItemWithFaces();
        TagDao maman = CreateTag("Maman");
        FaceDao face = FacesOf(item)[0];
        _faces.SetPersonTag(face, maman);
        _db.SaveChanges();

        _tags.DeleteTag(new Tag("Maman"));
        _db.SaveChanges();

        Assert.Empty(_db.FaceTags);
        Assert.Null(_faces.GetPersonTag(face.FaceId));
    }
}

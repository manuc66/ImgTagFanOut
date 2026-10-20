using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.Models.Faces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Tests;

public class FaceAnalysisCacheTests : IDisposable
{
    private const string ItemName = "photo.jpg";
    private const string ItemPath = "/photos/photo.jpg";

    private readonly SqliteConnection _connection;

    public FaceAnalysisCacheTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using ImgTagFanOutDbContext db = CreateContext();
        db.Database.EnsureCreated();
        db.Items.Add(new ItemDao(ItemName));
        db.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private ImgTagFanOutDbContext CreateContext()
    {
        DbContextOptions<ImgTagFanOutDbContext> options = new DbContextOptionsBuilder<ImgTagFanOutDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new ImgTagFanOutDbContext(options, new TagCache());
    }

    private FaceAnalysisOutcome Run(FakeAnalyzer analyzer, string hash = "hash-1")
    {
        using ImgTagFanOutDbContext db = CreateContext();
        ItemDao item = db.Items.Single(x => x.Name == ItemName);
        FaceAnalysisService service = new(analyzer);

        FaceAnalysisOutcome outcome = service
            .AnalyzeAsync(db, item, hash, ItemPath)
            .GetAwaiter()
            .GetResult();

        db.SaveChanges();
        return outcome;
    }

    [Fact]
    public void FirstAnalysis_DetectsAndEmbeds_AndPersistsEverything()
    {
        FakeAnalyzer analyzer = new();

        FaceAnalysisOutcome outcome = Run(analyzer);

        Assert.Equal(2, outcome.FaceCount);
        Assert.False(outcome.DetectedFromCache);
        Assert.False(outcome.EmbeddedFromCache);
        Assert.Equal(1, analyzer.DetectCalls);
        Assert.Equal(2, analyzer.EmbedCalls);

        using ImgTagFanOutDbContext db = CreateContext();
        FaceAnalysisDao analysis = Assert.Single(db.FaceAnalyses);
        Assert.Equal("hash-1", analysis.SourceHash);
        Assert.Equal("det-v1", analysis.DetectorVersion);
        Assert.Equal(2, analysis.FaceCount);

        List<FaceDao> faces = db.Faces.OrderBy(x => x.Position).ToList();
        Assert.Equal(2, faces.Count);
        Assert.All(faces, f =>
        {
            Assert.NotNull(f.Embedding);
            Assert.Equal("emb-v1", f.EmbedderVersion);
            Assert.Equal(FaceLandmarks.ValueCount, FaceLandmarks.Decode(f.Landmarks).Length);
        });
    }

    [Fact]
    public void SecondAnalysis_WithSameVersionsAndHash_DoesNotRecomputeAnything()
    {
        Run(new FakeAnalyzer());

        FakeAnalyzer analyzer = new();
        FaceAnalysisOutcome outcome = Run(analyzer);

        Assert.True(outcome.DetectedFromCache);
        Assert.True(outcome.EmbeddedFromCache);
        Assert.Equal(0, analyzer.DetectCalls);
        Assert.Equal(0, analyzer.EmbedCalls);
    }

    [Fact]
    public void DetectorVersionChange_RedetectsAndReembeds()
    {
        Run(new FakeAnalyzer());

        FakeAnalyzer analyzer = new() { DetectorVersion = "det-v2" };
        FaceAnalysisOutcome outcome = Run(analyzer);

        Assert.False(outcome.DetectedFromCache);
        Assert.Equal(1, analyzer.DetectCalls);
        Assert.Equal(2, analyzer.EmbedCalls);
    }

    [Fact]
    public void EmbedderVersionChange_ReusesDetectionsAndOnlyReembeds()
    {
        Run(new FakeAnalyzer());

        FakeAnalyzer analyzer = new() { EmbedderVersion = "emb-v2" };
        FaceAnalysisOutcome outcome = Run(analyzer);

        Assert.True(outcome.DetectedFromCache);
        Assert.False(outcome.EmbeddedFromCache);
        Assert.Equal(0, analyzer.DetectCalls);
        Assert.Equal(2, analyzer.EmbedCalls);

        using ImgTagFanOutDbContext db = CreateContext();
        Assert.All(db.Faces.ToList(), f => Assert.Equal("emb-v2", f.EmbedderVersion));
    }

    [Fact]
    public void ChangedFileHash_InvalidatesEverything()
    {
        Run(new FakeAnalyzer());

        FakeAnalyzer analyzer = new();
        FaceAnalysisOutcome outcome = Run(analyzer, hash: "hash-2");

        Assert.False(outcome.DetectedFromCache);
        Assert.Equal(1, analyzer.DetectCalls);
        Assert.Equal(2, analyzer.EmbedCalls);

        using ImgTagFanOutDbContext db = CreateContext();
        Assert.Equal("hash-2", db.FaceAnalyses.Single().SourceHash);
        Assert.Equal(2, db.Faces.Count());
    }

    [Fact]
    public void ImageWithoutFaces_IsNotAnalyzedTwice()
    {
        FakeAnalyzer analyzer = new() { FacesPerImage = 0 };
        FaceAnalysisOutcome first = Run(analyzer);

        Assert.Equal(0, first.FaceCount);
        Assert.Equal(1, analyzer.DetectCalls);

        FaceAnalysisOutcome second = Run(new FakeAnalyzer { FacesPerImage = 0 });

        Assert.True(second.DetectedFromCache);
        Assert.True(second.EmbeddedFromCache);

        using ImgTagFanOutDbContext db = CreateContext();
        Assert.Empty(db.Faces);
        Assert.Single(db.FaceAnalyses);
    }

    private sealed class FakeAnalyzer : IFaceAnalyzer
    {
        public string DetectorVersion { get; init; } = "det-v1";
        public string EmbedderVersion { get; init; } = "emb-v1";
        public int FacesPerImage { get; init; } = 2;

        public int DetectCalls { get; private set; }
        public int EmbedCalls { get; private set; }

        public Task<FaceAnalysisResult> AnalyzeAsync(
            string fullFilePath,
            IReadOnlyList<FaceDetection>? knownDetections,
            bool computeEmbeddings,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<FaceDetection> detections;
            if (knownDetections is null)
            {
                DetectCalls++;
                detections = Enumerable.Range(0, FacesPerImage)
                    .Select(i => new FaceDetection(
                        X: i * 10,
                        Y: i * 10,
                        Width: 50,
                        Height: 50,
                        Score: 0.99f,
                        Landmarks: Enumerable.Repeat((float)i, FaceLandmarks.ValueCount).ToArray()))
                    .ToArray();
            }
            else
            {
                detections = knownDetections;
            }

            List<AnalyzedFace> faces = new();
            foreach (FaceDetection detection in detections)
            {
                byte[]? embedding = null;
                if (computeEmbeddings)
                {
                    EmbedCalls++;
                    embedding = [1, 2, 3, 4];
                }

                faces.Add(new AnalyzedFace(detection, embedding));
            }

            return Task.FromResult(new FaceAnalysisResult(faces));
        }
    }
}

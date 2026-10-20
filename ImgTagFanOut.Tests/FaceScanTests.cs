using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.Models.Faces;
using ImgTagFanOut.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Tests;

public class FaceScanTests : IDisposable
{
    private readonly string _folder;
    private readonly SqliteConnection _connection;
    private readonly TestUnitOfWorkFactory _unitOfWorkFactory;

    public FaceScanTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "ImgTagFanOut-FaceScan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _unitOfWorkFactory = new TestUnitOfWorkFactory(_connection);

        // Premier contexte : crée le schéma SQLite en mémoire.
        using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(_folder);
    }

    public void Dispose()
    {
        _connection.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private void AddImage(string name)
    {
        File.WriteAllBytes(Path.Combine(_folder, name), [1, 2, 3]);
    }

    private void RegisterItems(params string[] names)
    {
        DbContextOptions<ImgTagFanOutDbContext> options = new DbContextOptionsBuilder<ImgTagFanOutDbContext>()
            .UseSqlite(_connection)
            .Options;
        using ImgTagFanOutDbContext db = new(options, new TagCache());
        foreach (string name in names)
        {
            db.Items.Add(new ItemDao(name));
        }

        db.SaveChanges();
    }

    private static ImgTagFanOutDbContext OpenContext(SqliteConnection connection)
    {
        DbContextOptions<ImgTagFanOutDbContext> options = new DbContextOptionsBuilder<ImgTagFanOutDbContext>()
            .UseSqlite(connection)
            .Options;
        return new ImgTagFanOutDbContext(options, new TagCache());
    }

    private static FaceScan CreateScan(
        IUnitOfWorkFactory factory,
        FakeAnalyzer analyzer,
        FakeHashEvaluator hashEvaluator)
    {
        return new FaceScan(factory, hashEvaluator, () => analyzer);
    }

    [Fact]
    public async Task Scan_AnalyzesEveryImageAndPersistsHashesAndFaces()
    {
        AddImage("a.jpg");
        AddImage("b.jpg");
        RegisterItems("a.jpg", "b.jpg");

        FakeAnalyzer analyzer = new();
        FakeHashEvaluator hashEvaluator = new();
        FaceScan scan = CreateScan(_unitOfWorkFactory, analyzer, hashEvaluator);

        FaceScanSummary summary = await scan.ScanAsync(_folder, progress: null, CancellationToken.None);

        Assert.Equal(2, summary.Total);
        Assert.Equal(2, summary.Analyzed);
        Assert.Equal(0, summary.Cached);
        Assert.Equal(4, summary.FaceCount);
        Assert.Equal(2, analyzer.DetectCalls);
        Assert.Equal(2, hashEvaluator.Calls);

        using ImgTagFanOutDbContext db = OpenContext(_connection);
        Assert.All(db.Items.ToList(), item => Assert.False(string.IsNullOrEmpty(item.Hash)));
        Assert.Equal(4, db.Faces.Count());
        Assert.Equal(2, db.FaceAnalyses.Count());
    }

    [Fact]
    public async Task SecondScan_DoesNotTouchTheModelsNorTheHashes()
    {
        AddImage("a.jpg");
        RegisterItems("a.jpg");

        await CreateScan(_unitOfWorkFactory, new FakeAnalyzer(), new FakeHashEvaluator())
            .ScanAsync(_folder, null, CancellationToken.None);

        FakeAnalyzer analyzer = new();
        FakeHashEvaluator hashEvaluator = new();

        FaceScanSummary summary = await CreateScan(_unitOfWorkFactory, analyzer, hashEvaluator)
            .ScanAsync(_folder, null, CancellationToken.None);

        Assert.Equal(1, summary.Analyzed);
        Assert.Equal(1, summary.Cached);
        Assert.Equal(0, analyzer.DetectCalls);
        Assert.Equal(0, analyzer.EmbedCalls);
        Assert.Equal(0, hashEvaluator.Calls);
    }

    [Fact]
    public async Task MissingFile_IsSkippedWithoutFailing()
    {
        AddImage("a.jpg");
        RegisterItems("a.jpg", "gone.jpg");

        FakeAnalyzer analyzer = new();
        FaceScanSummary summary = await CreateScan(_unitOfWorkFactory, analyzer, new FakeHashEvaluator())
            .ScanAsync(_folder, null, CancellationToken.None);

        Assert.Equal(2, summary.Total);
        Assert.Equal(1, summary.Analyzed);
        Assert.Equal(1, summary.Skipped);
        Assert.Equal(0, summary.Failures);
        Assert.Equal(1, analyzer.DetectCalls);
    }

    [Fact]
    public async Task OneBrokenImage_DoesNotStopTheOthers()
    {
        AddImage("good.jpg");
        AddImage("broken.jpg");
        RegisterItems("good.jpg", "broken.jpg");

        FakeAnalyzer analyzer = new() { ThrowOn = "broken.jpg" };
        FaceScanSummary summary = await CreateScan(_unitOfWorkFactory, analyzer, new FakeHashEvaluator())
            .ScanAsync(_folder, null, CancellationToken.None);

        Assert.Equal(1, summary.Analyzed);
        Assert.Equal(1, summary.Failures);
        Assert.Equal(1, analyzer.DetectCalls);
    }

    [Fact]
    public async Task Cancellation_KeepsWhatWasAlreadyComputed()
    {
        AddImage("a.jpg");
        AddImage("b.jpg");
        AddImage("c.jpg");
        RegisterItems("a.jpg", "b.jpg", "c.jpg");

        FakeAnalyzer analyzer = new();
        using CancellationTokenSource cts = new();
        SynchronousProgress<FaceScanProgress> progress = new(report =>
        {
            if (report.Processed >= 2)
            {
                cts.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateScan(_unitOfWorkFactory, analyzer, new FakeHashEvaluator())
                .ScanAsync(_folder, progress, cts.Token));

        using ImgTagFanOutDbContext db = OpenContext(_connection);
        Assert.Equal(2, db.FaceAnalyses.Count());
        Assert.Equal(4, db.Faces.Count());
    }

    /// <summary>Unlike <see cref="Progress{T}"/>, does not post to the pool: keeps the test deterministic.</summary>
    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        public SynchronousProgress(Action<T> report)
        {
            _report = report;
        }

        public void Report(T value) => _report(value);
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

    private sealed class FakeHashEvaluator : IHashEvaluator
    {
        public int Calls { get; private set; }

        public Task<string> ComputeHashAsync(string filePath, CancellationToken ctsToken)
        {
            Calls++;
            return Task.FromResult("hash:" + Path.GetFileName(filePath));
        }
    }

    private sealed class FakeAnalyzer : IFaceAnalyzer
    {
        public string DetectorVersion => "det-test";
        public string EmbedderVersion => "emb-test";
        public string? ThrowOn { get; init; }

        public int DetectCalls { get; private set; }
        public int EmbedCalls { get; private set; }

        public Task<FaceAnalysisResult> AnalyzeAsync(
            string fullFilePath,
            IReadOnlyList<FaceDetection>? knownDetections,
            bool computeEmbeddings,
            CancellationToken cancellationToken)
        {
            if (ThrowOn is not null && Path.GetFileName(fullFilePath) == ThrowOn)
            {
                throw new InvalidOperationException("image illisible");
            }

            IReadOnlyList<FaceDetection> detections = knownDetections ?? CreateDetections();
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

        private IReadOnlyList<FaceDetection> CreateDetections()
        {
            DetectCalls++;
            return
            [
                new FaceDetection(0.1f, 0.1f, 0.2f, 0.2f, 0.9f, new float[FaceLandmarks.ValueCount]),
                new FaceDetection(0.5f, 0.5f, 0.2f, 0.2f, 0.9f, new float[FaceLandmarks.ValueCount]),
            ];
        }

        public void Dispose()
        {
        }
    }
}

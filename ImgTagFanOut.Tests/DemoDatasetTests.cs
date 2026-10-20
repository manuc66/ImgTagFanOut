using System.Text.Json;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models.Faces;
using ImgTagFanOut.ViewModels;
using Microsoft.Data.Sqlite;

namespace ImgTagFanOut.Tests;

/// <summary>
/// End-to-end run on the demo portraits: real models, real photos, several
/// pictures per person. Slow (a few seconds) but it is the only test that
/// exercises detection, embeddings, storage and suggestions together.
/// </summary>
public class DemoDatasetTests : IDisposable
{
    private static string DemoFolder => Path.Combine(AppContext.BaseDirectory, "demo", "faces");

    private static string ModelsFolder => Path.Combine(AppContext.BaseDirectory, "Assets", "Models");

    private readonly SqliteConnection _connection;
    private readonly InMemoryUnitOfWorkFactory _unitOfWorkFactory;

    public DemoDatasetTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _unitOfWorkFactory = new InMemoryUnitOfWorkFactory(_connection);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private static List<string> Photos()
    {
        return Directory
            .GetFiles(DemoFolder, "*.jpg", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(DemoFolder, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static Dictionary<string, List<string>> Persons()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(DemoFolder, "manifest.json")));

        Dictionary<string, List<string>> persons = new(StringComparer.Ordinal);
        foreach (JsonProperty person in manifest.RootElement.GetProperty("persons").EnumerateObject())
        {
            persons[person.Name] = person.Value
                .EnumerateArray()
                .Select(item => item.GetProperty("file").GetString()!.Replace('/', Path.DirectorySeparatorChar))
                .ToList();
        }

        return persons;
    }

    /// <summary>The portrait is the largest face of the photo; a framed picture in the background is not.</summary>
    private static FaceDao MainFace(IUnitOfWork unitOfWork, string file)
    {
        return unitOfWork.FaceRepository
            .GetFacesWithPerson(file)
            .Select(row => row.Face)
            .OrderByDescending(face => face.Width * face.Height)
            .First();
    }

    [Fact]
    public async Task DemoPortraits_AreDetectedAnalyzedAndRecognized()
    {
        Assert.True(Directory.Exists(DemoFolder), $"demo folder missing: {DemoFolder}");
        Assert.True(Directory.Exists(ModelsFolder), $"models missing: {ModelsFolder}");

        List<string> photos = Photos();
        Assert.Equal(17, photos.Count);

        using (IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(DemoFolder))
        {
            foreach (string photo in photos)
            {
                unitOfWork.AsDbContext().Items.Add(new ItemDao(photo));
            }

            unitOfWork.SaveChanges();
        }

        FaceScan scan = new(
            _unitOfWorkFactory,
            new HashEvaluator(),
            () => new OpenCvFaceAnalyzer(ModelsFolder));

        FaceScanSummary summary = await scan.ScanAsync(DemoFolder, progress: null, CancellationToken.None);

        Assert.False(summary.AnalyzerUnavailable);
        Assert.Equal(photos.Count, summary.Analyzed);
        Assert.Equal(0, summary.Failures);

        // Un portrait peut contenir un visage de plus (photo dans le décor) : on
        // vérifie qu'aucune photo n'est passée à côté, pas le compte exact.
        Assert.True(
            summary.FaceCount >= photos.Count,
            $"{summary.FaceCount} visages pour {photos.Count} photos");

        // Chaque personne est nommée sur sa première photo, les autres servent de requêtes.
        Dictionary<string, List<string>> persons = Persons();
        PersonSuggester suggester = new(_unitOfWorkFactory);

        using (IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(DemoFolder))
        {
            foreach ((string name, List<string> files) in persons)
            {
                unitOfWork.TagRepository.TryCreateTag(name, out _);
                unitOfWork.SaveChanges();

                FaceDao face = MainFace(unitOfWork, files[0]);
                unitOfWork.FaceRepository.SetPersonTag(face, unitOfWork.TagRepository.GetTagDao(name));
                unitOfWork.SaveChanges();
            }
        }

        suggester.Invalidate(DemoFolder);

        List<string> failures = new();
        int queries = 0;

        using (IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(DemoFolder))
        {
            foreach ((string name, List<string> files) in persons)
            {
                foreach (string file in files.Skip(1))
                {
                    queries++;
                    FaceDao face = MainFace(unitOfWork, file);
                    IReadOnlyList<PersonMatch> matches = suggester.Suggest(DemoFolder, face.Embedding!);

                    if (matches.Count == 0 || matches[0].PersonName != name)
                    {
                        failures.Add($"{file}: attendu {name}, obtenu {(matches.Count == 0 ? "rien" : matches[0].PersonName)}");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count}/{queries} erreurs :\n" + string.Join("\n", failures));
    }
}

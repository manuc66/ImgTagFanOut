using ImgTagFanOut.Dao;
using ImgTagFanOut.Models.Suggestions;

namespace ImgTagFanOut.Tests;

public class TagSuggestionEngineTests
{
    private readonly TagSuggestionEngine _engine = new();

    private static readonly IReadOnlySet<string> NoTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<TagSuggestionRecord> Suggest(
        string itemName,
        string[] vocabulary,
        string[]? assigned = null,
        string[]? people = null,
        float[]? embedding = null,
        LabelledEmbedding[]? gallery = null,
        string[]? rejected = null)
    {
        return _engine.Suggest(
            itemName,
            1,
            new HashSet<string>(assigned ?? [], StringComparer.OrdinalIgnoreCase),
            people ?? [],
            embedding,
            vocabulary,
            gallery ?? [],
            new HashSet<string>(rejected ?? [], StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void PathSegment_ProposesTheTag()
    {
        TagSuggestionRecord suggestion = Assert.Single(Suggest("2024/Vacances/IMG_0001.jpg", ["Vacances", "Famille"]));

        Assert.Equal("Vacances", suggestion.TagName);
        Assert.Equal(TagSuggestionSource.Path, suggestion.Source);
        Assert.Equal(0.85, suggestion.Score, 3);
    }

    [Fact]
    public void PathMatching_IgnoresCaseAndAccents()
    {
        TagSuggestionRecord suggestion = Assert.Single(Suggest("2023/noel/IMG_1.jpg", ["Noël"]));

        Assert.Equal("Noël", suggestion.TagName);
    }

    [Fact]
    public void PathPhrase_MatchesInsideAFileName()
    {
        TagSuggestionRecord suggestion = Assert.Single(Suggest("sorties/repas-de-famille.jpg", ["Famille"]));

        Assert.Equal("Famille", suggestion.TagName);
    }

    [Fact]
    public void PathEvidence_DoesNotInventTagsOutsideTheVocabulary()
    {
        IReadOnlyList<TagSuggestionRecord> suggestions = Suggest("2024/Vacances/IMG_1.jpg", ["Travail"]);

        Assert.Empty(suggestions);
    }

    [Fact]
    public void RecognisedFace_ProposesThePerson()
    {
        TagSuggestionRecord suggestion = Assert.Single(
            Suggest("IMG_1.jpg", ["Maman", "Papa"], people: ["Maman"]));

        Assert.Equal("Maman", suggestion.TagName);
        Assert.Equal(TagSuggestionSource.Face, suggestion.Source);
        Assert.Equal(0.95, suggestion.Score, 3);
    }

    [Fact]
    public void TagsAlreadyOnTheImage_AreNotProposedAgain()
    {
        IReadOnlyList<TagSuggestionRecord> suggestions = Suggest(
            "2024/Vacances/IMG_1.jpg",
            ["Vacances"],
            assigned: ["Vacances"]);

        Assert.Empty(suggestions);
    }

    [Fact]
    public void RejectedTag_IsNotProposedAgain()
    {
        IReadOnlyList<TagSuggestionRecord> suggestions = Suggest(
            "2024/Vacances/IMG_1.jpg",
            ["Vacances"],
            rejected: ["Vacances"]);

        Assert.Empty(suggestions);
    }

    [Fact]
    public void SimilarityColdStart_StaysSilentWithOneTag()
    {
        LabelledEmbedding[] gallery =
        [
            new(2, [1f, 0f, 0f], ["Vacances"]),
            new(3, [1f, 0.01f, 0f], ["Vacances"]),
            new(4, [1f, 0f, 0.01f], ["Vacances"]),
        ];

        IReadOnlyList<TagSuggestionRecord> suggestions = Suggest(
            "IMG_1.jpg",
            ["Vacances"],
            embedding: [1f, 0f, 0f],
            gallery: gallery);

        Assert.Empty(suggestions);
    }

    [Fact]
    public void Similarity_ProposesTheDominantTag()
    {
        LabelledEmbedding[] gallery =
        [
            new(2, [1f, 0.01f, 0f], ["Plage"]),
            new(3, [1f, 0f, 0.02f], ["Plage"]),
            new(4, [0.5f, 1f, 0f], ["Montagne"]),
            new(5, [0.4f, 1f, 0f], ["Montagne"]),
            new(6, [0.2f, 0.1f, 1f], ["Bureau"]),
            new(7, [0.2f, 0.2f, 1f], ["Bureau"]),
        ];

        IReadOnlyList<TagSuggestionRecord> suggestions = Suggest(
            "IMG_1.jpg",
            ["Plage", "Montagne", "Bureau"],
            embedding: [1f, 0.02f, 0f],
            gallery: gallery);

        TagSuggestionRecord suggestion = Assert.Single(suggestions);
        Assert.Equal("Plage", suggestion.TagName);
        Assert.Equal(TagSuggestionSource.Similar, suggestion.Source);
        Assert.InRange(suggestion.Score, TagSuggestionEngine.MinimumScore, 0.75);
    }

    [Fact]
    public void Similarity_IgnoresTheImageItself()
    {
        LabelledEmbedding[] gallery =
        [
            new(1, [1f, 0f, 0f], ["Déjà là"]),
            new(2, [1f, 0.01f, 0f], ["Vacances"]),
            new(3, [1f, 0f, 0.02f], ["Vacances"]),
            new(4, [0.5f, 1f, 0f], ["Travail"]),
            new(5, [0.4f, 1f, 0f], ["Travail"]),
            new(6, [0.2f, 0.1f, 1f], ["Maison"]),
            new(7, [0.2f, 0.2f, 1f], ["Maison"]),
        ];

        IReadOnlyList<TagSuggestionRecord> suggestions = Suggest(
            "IMG_1.jpg",
            ["Déjà là", "Vacances", "Travail", "Maison"],
            assigned: ["Déjà là"],
            embedding: [1f, 0f, 0f],
            gallery: gallery);

        Assert.DoesNotContain(suggestions, suggestion => suggestion.TagName == "Déjà là");
    }

    [Fact]
    public void Suggestions_AreCappedAndOrderedByScore()
    {
        IReadOnlyList<TagSuggestionRecord> suggestions = Suggest(
            "vacances/famille/plage/IMG_1.jpg",
            ["Vacances", "Famille", "Plage"],
            people: ["Maman"]);

        Assert.True(suggestions.Count <= TagSuggestionEngine.MaxSuggestions);
        Assert.Equal(suggestions.OrderByDescending(suggestion => suggestion.Score).Select(suggestion => suggestion.TagName),
            suggestions.Select(suggestion => suggestion.TagName));
    }

    [Fact]
    public void Normalize_RemovesAccentsAndCollapsesSeparators()
    {
        Assert.Equal("noel 2024", TagSuggestionEngine.Normalize("  Noël -- 2024  "));
        Assert.Equal("ete a l ile", TagSuggestionEngine.Normalize("Été à l'île"));
    }

    [Fact]
    public void EmptyVocabulary_ReturnsNothing()
    {
        Assert.Empty(Suggest("vacances/IMG_1.jpg", []));
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ImgTagFanOut.Dao;

namespace ImgTagFanOut.Models.Suggestions;

/// <summary>Stable identifiers stored with a suggestion, used to build its reason.</summary>
public static class TagSuggestionSource
{
    public const string Path = "path";
    public const string Face = "face";
    public const string Similar = "similar";
}

/// <summary>
/// Scores the tags of the user's vocabulary against one image. It is a
/// decision model, not a generative one: every score comes from evidence the
/// user can inspect (the path, a named face, visually similar tagged photos),
/// so the same image always gets the same suggestions.
/// </summary>
/// <remarks>
/// The similarity score is deliberately conservative. A kNN share is not a
/// calibrated probability, and a gallery with a single tag would make every
/// photo look certain. The engine therefore stays silent until the library
/// holds at least <see cref="MinDistinctTags"/> different tags and
/// <see cref="MinExamplesPerTag"/> examples of the winning tag.
/// </remarks>
public sealed class TagSuggestionEngine
{
    public const double MinimumScore = 0.45;
    public const int MaxSuggestions = 3;

    private const double PathSegmentScore = 0.85;
    private const double PathTokensScore = 0.65;
    private const double FaceScore = 0.95;

    private const double SimilarityFloor = 0.72;
    private const double SimilarityTemperature = 0.02;
    private const int SimilarityNeighbours = 12;
    private const int MinDistinctTags = 3;
    private const int MinExamplesPerTag = 1;
    private const double MinSimilarShare = 0.30;
    private const double MaxSimilarScore = 0.75;

    /// <summary>File-name words that say nothing about the content.</summary>
    private static readonly HashSet<string> NoiseTokens = new(StringComparer.Ordinal)
    {
        "img", "image", "photo", "picture", "dsc", "dscn", "dcim", "screenshot",
        "jpg", "jpeg", "jpe", "png", "gif", "webp", "bmp", "heic", "heif", "tif", "tiff", "cr2",
        "copy", "final", "edit", "edited", "export",
    };

    public IReadOnlyList<TagSuggestionRecord> Suggest(
        string itemName,
        int itemId,
        IReadOnlySet<string> assignedTags,
        IReadOnlyList<string> personNames,
        IReadOnlyList<float>? embedding,
        IReadOnlyList<string> vocabulary,
        IReadOnlyList<LabelledEmbedding> gallery,
        IReadOnlySet<string>? rejectedTags = null)
    {
        Dictionary<string, string> canonical = BuildCanonicalVocabulary(vocabulary);
        if (canonical.Count == 0)
        {
            return [];
        }

        Dictionary<string, Candidate> candidates = new(StringComparer.Ordinal);

        void Consider(string tagName, double score, string source, string reasonArgument)
        {
            if (!canonical.TryGetValue(Normalize(tagName), out string? tag))
            {
                return;
            }

            if (assignedTags.Contains(tag)
                || (rejectedTags is not null && rejectedTags.Contains(tag)))
            {
                return;
            }

            if (!candidates.TryGetValue(tag, out Candidate? candidate))
            {
                candidates[tag] = new Candidate(score, source, reasonArgument);
                return;
            }

            if (score > candidate.Score)
            {
                candidates[tag] = new Candidate(score, source, reasonArgument);
            }
        }

        foreach ((string tag, double score) in ScorePath(itemName, canonical.Keys))
        {
            Consider(tag, score, TagSuggestionSource.Path, PathReasonArgument(itemName));
        }

        foreach (string person in personNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Consider(person, FaceScore, TagSuggestionSource.Face, person);
        }

        if (embedding is not null)
        {
            foreach (SimilarCandidate similar in ScoreSimilar(embedding, itemId, gallery, canonical, assignedTags, rejectedTags))
            {
                Consider(similar.TagName, similar.Score, TagSuggestionSource.Similar, similar.Count.ToString(CultureInfo.InvariantCulture));
            }
        }

        return candidates
            .Select(pair => new TagSuggestionRecord(pair.Key, pair.Value.Score, pair.Value.Source, pair.Value.ReasonArgument))
            .Where(suggestion => suggestion.Score >= MinimumScore)
            .OrderByDescending(suggestion => suggestion.Score)
            .ThenBy(suggestion => suggestion.TagName, StringComparer.OrdinalIgnoreCase)
            .Take(MaxSuggestions)
            .ToArray();
    }

    private static Dictionary<string, string> BuildCanonicalVocabulary(IReadOnlyList<string> vocabulary)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (string name in vocabulary)
        {
            string normalized = Normalize(name);
            if (normalized.Length == 0 || NoiseTokens.Contains(normalized))
            {
                continue;
            }

            // Two tags that normalise to the same words are indistinguishable
            // here; keep the first one so the output stays deterministic.
            result.TryAdd(normalized, name);
        }

        return result;
    }

    private static IEnumerable<(string Tag, double Score)> ScorePath(string itemName, IEnumerable<string> normalizedTags)
    {
        string[] segments = itemName
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(segment => segment.Length > 0)
            .ToArray();

        if (segments.Length == 0)
        {
            yield break;
        }

        string pathPhrase = string.Join(' ', segments);
        HashSet<string> pathTokens = segments.SelectMany(Tokenize).Where(IsMeaningfulToken).ToHashSet(StringComparer.Ordinal);

        foreach (string normalizedTag in normalizedTags)
        {
            string tagPhrase = normalizedTag;
            string[] tagTokens = Tokenize(normalizedTag).Where(IsMeaningfulToken).ToArray();

            if (tagTokens.Length == 0)
            {
                continue;
            }

            bool exactSegment = segments.Any(segment => segment == tagPhrase);
            if (exactSegment)
            {
                yield return (normalizedTag, PathSegmentScore);
                continue;
            }

            bool phraseContained = ContainsPhrase(pathPhrase, tagPhrase);
            bool allTokens = tagTokens.All(pathTokens.Contains);
            if (phraseContained || allTokens)
            {
                yield return (normalizedTag, PathTokensScore);
            }
        }
    }

    private sealed record SimilarCandidate(string TagName, double Score, double Share, int Count);

    private static IEnumerable<SimilarCandidate> ScoreSimilar(
        IReadOnlyList<float> embedding,
        int itemId,
        IReadOnlyList<LabelledEmbedding> gallery,
        IReadOnlyDictionary<string, string> canonical,
        IReadOnlySet<string> assignedTags,
        IReadOnlySet<string>? rejectedTags)
    {
        if (embedding.Count == 0)
        {
            yield break;
        }

        HashSet<string> distinctTags = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> examples = new(StringComparer.OrdinalIgnoreCase);
        foreach (LabelledEmbedding labelled in gallery)
        {
            foreach (string tag in labelled.Tags)
            {
                distinctTags.Add(tag);
                examples[tag] = examples.GetValueOrDefault(tag) + 1;
            }
        }

        if (distinctTags.Count < MinDistinctTags)
        {
            yield break;
        }

        List<(double Similarity, LabelledEmbedding Image)> neighbours = [];
        foreach (LabelledEmbedding labelled in gallery)
        {
            if (labelled.ItemId == itemId || labelled.Embedding.Length != embedding.Count)
            {
                continue;
            }

            neighbours.Add((Cosine(embedding, labelled.Embedding), labelled));
        }

        neighbours = neighbours
            .OrderByDescending(neighbour => neighbour.Similarity)
            .Take(SimilarityNeighbours)
            .ToList();

        if (neighbours.Count == 0 || neighbours[0].Similarity < SimilarityFloor)
        {
            yield break;
        }

        double bestSimilarity = neighbours[0].Similarity;
        double totalWeight = 0;
        Dictionary<string, (double Weight, int Count)> scores = new(StringComparer.OrdinalIgnoreCase);

        foreach ((double similarity, LabelledEmbedding image) in neighbours)
        {
            double weight = Math.Exp((similarity - bestSimilarity) / SimilarityTemperature);
            totalWeight += weight;

            foreach (string tag in image.Tags.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!canonical.TryGetValue(Normalize(tag), out string? tagName)
                    || assignedTags.Contains(tagName)
                    || (rejectedTags is not null && rejectedTags.Contains(tagName)))
                {
                    continue;
                }

                (double current, int count) = scores.GetValueOrDefault(tagName);
                scores[tagName] = (current + weight, count + 1);
            }
        }

        if (totalWeight <= 0)
        {
            yield break;
        }

        List<SimilarCandidate> ranked = scores
            .Where(pair => examples.GetValueOrDefault(pair.Key) >= MinExamplesPerTag)
            .Select(pair =>
            {
                double share = pair.Value.Weight / totalWeight;
                double score = Math.Min(MaxSimilarScore, 0.45 + 0.30 * share);
                return new SimilarCandidate(pair.Key, score, share, pair.Value.Count);
            })
            .Where(candidate => candidate.Score >= MinimumScore)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.TagName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ranked.Count == 0)
        {
            yield break;
        }

        // A single neighbour that happens to carry the tag is not enough: the
        // winner must own a fair share of the neighbourhood and beat the next
        // tag by a visible margin.
        SimilarCandidate winner = ranked[0];
        double runnerUpShare = ranked.Count > 1 ? ranked[1].Share : 0;
        if (winner.Share < MinSimilarShare || winner.Share - runnerUpShare < 0.15)
        {
            yield break;
        }

        yield return winner;

        if (ranked.Count > 1 && ranked[1].Share >= MinSimilarShare)
        {
            yield return ranked[1];
        }
    }

    private static double Cosine(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;

        for (int i = 0; i < left.Count; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }

        if (leftNorm <= 0 || rightNorm <= 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }

    private static string PathReasonArgument(string itemName)
    {
        string[] segments = itemName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length > 0 ? segments[^1] : itemName;
    }

    private static bool IsMeaningfulToken(string token)
    {
        return token.Length >= 3 && !NoiseTokens.Contains(token) && !token.All(char.IsDigit);
    }

    private static IEnumerable<string> Tokenize(string normalized)
    {
        int start = -1;
        for (int i = 0; i <= normalized.Length; i++)
        {
            bool separator = i == normalized.Length || !char.IsLetterOrDigit(normalized[i]);
            if (!separator && start < 0)
            {
                start = i;
            }
            else if (separator && start >= 0)
            {
                yield return normalized[start..i];
                start = -1;
            }
        }
    }

    private static bool ContainsPhrase(string haystack, string needle)
    {
        if (needle.Length == 0)
        {
            return false;
        }

        int index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            bool before = index == 0 || !char.IsLetterOrDigit(haystack[index - 1]);
            int end = index + needle.Length;
            bool after = end == haystack.Length || !char.IsLetterOrDigit(haystack[end]);
            if (before && after)
            {
                return true;
            }

            index++;
        }

        return false;
    }

    /// <summary>Lowercase, accent-free, with every non-alphanumeric run collapsed to one space.</summary>
    public static string Normalize(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(decomposed.Length);

        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
            else if (builder.Length > 0 && builder[^1] != ' ')
            {
                builder.Append(' ');
            }
        }

        return builder.ToString().Trim();
    }

    private sealed record Candidate(double Score, string Source, string ReasonArgument);
}

using System;
using System.Globalization;
using System.Reactive;
using System.Threading.Tasks;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models.Suggestions;
using ImgTagFanOut.Resources;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

/// <summary>One proposed tag, with the evidence that produced it.</summary>
public sealed class TagSuggestionItemViewModel
{
    public TagSuggestionItemViewModel(
        TagSuggestionDao suggestion,
        Func<TagSuggestionItemViewModel, Task> accept,
        Func<TagSuggestionItemViewModel, Task> reject)
    {
        TagName = suggestion.TagName;
        Score = suggestion.Score;
        ScoreText = suggestion.Score.ToString("P0", CultureInfo.CurrentCulture);
        Reason = BuildReason(suggestion);

        AcceptCommand = ReactiveCommand.CreateFromTask(() => accept(this));
        RejectCommand = ReactiveCommand.CreateFromTask(() => reject(this));
    }

    public string TagName { get; }

    public double Score { get; }

    public string ScoreText { get; }

    public string Reason { get; }

    public ReactiveCommand<Unit, Unit> AcceptCommand { get; }

    public ReactiveCommand<Unit, Unit> RejectCommand { get; }

    private static string BuildReason(TagSuggestionDao suggestion)
    {
        return suggestion.Source switch
        {
            TagSuggestionSource.Path => string.Format(Resources.Resources.TagSuggestionFromPath, suggestion.ReasonArgument),
            TagSuggestionSource.Face => string.Format(Resources.Resources.TagSuggestionFromFace, suggestion.ReasonArgument),
            TagSuggestionSource.Similar => string.Format(Resources.Resources.TagSuggestionFromSimilar, suggestion.ReasonArgument),
            _ => string.Empty,
        };
    }
}

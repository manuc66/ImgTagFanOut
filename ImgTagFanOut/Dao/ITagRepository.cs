using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Dao;

internal interface ITagRepository
{
    bool TryCreateTag(string tagName, [MaybeNullWhen(false)] out Tag newTag);
    ImmutableList<Tag> GetAllTag();
    void AddOrUpdateItem(CanHaveTag tagAssignation);
    void AddTagToItem(Tag tag, CanHaveTag tagAssignation);
    void RemoveTagToItem(Tag tag, CanHaveTag tagAssignation);
    void ToggleToItem(string tagName, CanHaveTag tagAssignation);
    void ToggleToItem(Tag tag, CanHaveTag tagAssignation);
    void MarkDone(CanHaveTag tagAssignation);
    void DeleteTag(Tag tag);
    ImmutableList<string> GetItemsWithTag(Tag tag);
}
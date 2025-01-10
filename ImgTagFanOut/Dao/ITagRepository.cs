using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Dao;

public interface ITagRepository
{
    bool TryCreateTag(string tagName, [MaybeNullWhen(false)] out Tag newTag);
    ImmutableList<Tag> GetAllTag();
    Task AddOrUpdateItem(CanHaveTag tagAssignation, Func<CanHaveTag, Task<string>> getHash);
    void AddTagToItem(Tag tag, CanHaveTag tagAssignation);
    void RemoveTagToItem(Tag tag, CanHaveTag tagAssignation);
    void ToggleToItem(string tagName, CanHaveTag tagAssignation);
    void ToggleToItem(Tag tag, CanHaveTag tagAssignation);
    void MarkDone(CanHaveTag tagAssignation);
    void MarkUnDone(CanHaveTag tagAssignation);
    void DeleteTag(Tag tag);
    ImmutableList<string> GetItemsWithTag(Tag tag);
    ImmutableList<Tag> GetAllTagForHash(string hash);
}

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Dao;

internal interface ITagRepository
{
    bool TryCreateTag(string tagName, [MaybeNullWhen(false)] out Tag newTag);
    ImmutableList<Tag> GetAll();
    void AddItem(CanHaveTag tagAssignation);
    void AddTagToItem(string tagName, CanHaveTag tagAssignation);
    void RemoveTagToItem(string tagName, CanHaveTag tagAssignation);
    void ToggleToItem(string tagName, CanHaveTag tagAssignation);
    void ToggleToItem(Tag tagName, CanHaveTag tagAssignation);
    void CleanAll();
}
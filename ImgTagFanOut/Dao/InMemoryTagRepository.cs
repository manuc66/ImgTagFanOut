using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Dao;

class InMemoryTagRepository : ITagRepository
{
    private readonly HashSet<Tag> _tags = new(Tag.Comparer);

    public bool TryCreateTag(string? tagName, [MaybeNullWhen(false)] out Tag newTag)
    {
        if (!string.IsNullOrWhiteSpace(tagName?.Trim()))
        {
            newTag = new Tag(tagName.Trim());
            bool added = _tags.Add(newTag);
            if (added)
            {
                return true;
            }
        }

        newTag = null;
        return false;
    }

    public ImmutableList<Tag> GetAll()
    {
        return _tags.ToImmutableList();
    }

    public void AddOrUpdateItem(CanHaveTag tagAssignation)
    {

    }

    public void AddTagToItem(string tagName, CanHaveTag tagAssignation)
    {
        if (_tags.TryGetValue(new Tag(tagName), out Tag? existingTag))
        {
            tagAssignation.AddTag(existingTag);
        }
    }

    public void RemoveTagToItem(string tagName, CanHaveTag? tagAssignation)
    {
        if (_tags.TryGetValue(new Tag(tagName), out Tag? existingTag))
        {
            tagAssignation?.RemoveTag(existingTag);
        }
    }

    public void ToggleToItem(string tagName, CanHaveTag tagAssignation)
    {
        ToggleToItem(new Tag(tagName), tagAssignation);
    }

    public void ToggleToItem(Tag tagName, CanHaveTag tagAssignation)
    {
        if (_tags.TryGetValue(tagName, out Tag? existingTag))
        {
            tagAssignation.Toggle(existingTag);
        }
    }
}
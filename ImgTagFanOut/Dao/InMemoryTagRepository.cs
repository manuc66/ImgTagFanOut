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
            newTag = new(tagName.Trim());
            bool added = _tags.Add(newTag);
            if (added)
            {
                return true;
            }
        }

        newTag = null;
        return false;
    }

    public ImmutableList<Tag> GetAllTag()
    {
        return _tags.ToImmutableList();
    }

    public void AddOrUpdateItem(CanHaveTag tagAssignation)
    {

    }

    public void AddTagToItem(Tag tag, CanHaveTag tagAssignation)
    {
        if (_tags.TryGetValue(tag, out Tag? existingTag))
        {
            tagAssignation.AddTag(existingTag);
        }
    }

    public void RemoveTagToItem(Tag tag, CanHaveTag? tagAssignation)
    {
        if (_tags.TryGetValue(tag, out Tag? existingTag))
        {
            tagAssignation?.RemoveTag(existingTag);
        }
    }

    public void ToggleToItem(string tagName, CanHaveTag tagAssignation)
    {
        ToggleToItem(new Tag(tagName), tagAssignation);
    }

    public void ToggleToItem(Tag tag, CanHaveTag tagAssignation)
    {
        if (_tags.TryGetValue(tag, out Tag? existingTag))
        {
            tagAssignation.Toggle(existingTag);
        }
    }

    public void MarkDone(CanHaveTag tagAssignation)
    {
        tagAssignation.Done = true;
    }

    public void DeleteTag(Tag tag)
    {
        if (_tags.TryGetValue(tag, out Tag? existingTag))
        {
            _tags.Remove(existingTag);
        }
    }

    public ImmutableList<string> GetItemsWithTag(Tag tag)
    {
        return new List<string>().ToImmutableList();
    }

    public ImmutableList<Tag> GetAllTagForHash(string hash)
    {
        return new List<Tag>().ToImmutableList();
    }
}
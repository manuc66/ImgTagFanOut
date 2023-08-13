using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace ImgTagFanOut.ViewModels;

class TagRepository
{
    private readonly HashSet<Tag> _tags = new HashSet<Tag>(Tag.Comparer);

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

    public void AddTagToItem<T>(string tagName, CanHaveTag<T> tagAssignation)
    {
        if (_tags.TryGetValue(new Tag(tagName), out Tag? existingTag))
        {
            tagAssignation.AddTag(existingTag);
        }
    }

    public void RemoveTagToItem<T>(string tagName, CanHaveTag<T>? tagAssignation)
    {
        if (_tags.TryGetValue(new Tag(tagName), out Tag? existingTag))
        {
            tagAssignation?.RemoveTag(existingTag);
        }
    }

    public void ToggleToItem(string tagName, CanHaveTag<string> tagAssignation)
    {
        ToggleToItem(new Tag(tagName), tagAssignation);
    }

    public void ToggleToItem(Tag tagName, CanHaveTag<string> tagAssignation)
    {
        if (_tags.TryGetValue(tagName, out Tag? existingTag))
        {
            tagAssignation.Toggle(existingTag);
        }
    }
}
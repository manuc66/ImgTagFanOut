using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ImgTagFanOut.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Dao;

public class TagRepository : ITagRepository
{
    private readonly IImgTagFanOutDbContext _dbContext;
    private readonly ITagCache _tagCache;

    public TagRepository(IImgTagFanOutDbContext dbContext, ITagCache tagCache)
    {
        _dbContext = dbContext;
        _tagCache = tagCache;
    }

    public bool TryCreateTag(string tagName, [MaybeNullWhen(false)] out Tag newTag)
    {
        string newTagLabel = tagName.Trim();

        if (!string.IsNullOrWhiteSpace(newTagLabel)
            && _dbContext.Tags.FirstOrDefault(x => x.Name == newTagLabel) == null)
        {
            TagDao tagDao = new() { Name = newTagLabel };
            newTag = _tagCache.GetOrCreate(tagDao);
            _dbContext.Tags.Add(tagDao);
            return true;
        }

        newTag = null;
        return false;
    }



    public ImmutableList<Tag> GetAllTag()
    {
        return _dbContext.Tags.Select(x => _tagCache.GetOrCreate(x)).ToImmutableList();
    }

    public void AddOrUpdateItem(CanHaveTag tagAssignation)
    {
        ItemDao? existingItem = _dbContext.Items
            .Include(i => i.ItemTags)
            .ThenInclude(x => x.Tag)
            .FirstOrDefault(t => t.Name == tagAssignation.Item);
        if (existingItem != null)
        {
            foreach (ItemTagDao itemTagDao in existingItem.ItemTags.OrderBy(x => x.OrderIndex))
            {
                tagAssignation.AddTag(_tagCache.GetOrCreate(itemTagDao.Tag));
            }

            tagAssignation.Done = existingItem.Done;
        }
        else
        {
            _dbContext.Items.Add(new ItemDao { Name = tagAssignation.Item });
        }
    }

    public void AddTagToItem(Tag tag, CanHaveTag tagAssignation)
    {
        TagDao? existingTag = _dbContext.Tags.FirstOrDefault(t => t.Name == tag.Name.Trim());
        if (existingTag == null) return;

        ItemDao? existingItem = _dbContext.Items
            .Include(x => x.Tags)
            .FirstOrDefault(t => t.Name == tagAssignation.Item);
        if (existingItem == null) return;

        if (existingItem.Tags.Any(x => x.TagId == existingTag.TagId))
        {
            return;
        }

        existingItem.Tags.Add(existingTag);
        if (existingTag.Items.Any(x => x.ItemId == existingItem.ItemId))
        {
            return;
        }

        existingTag.Items.Add(existingItem);
        _dbContext.ItemTags.Add(
            new ItemTagDao
            {
                ItemForeignKey = existingItem.ItemId,
                TagForeignKey = existingTag.TagId,
                Tag = existingTag,
                Item = existingItem,
                OrderIndex = existingItem.Tags.Count - 1
            });
        tagAssignation.AddTag(_tagCache.GetOrCreate(existingTag));
    }

    public void RemoveTagToItem(Tag tag, CanHaveTag tagAssignation)
    {
        TagDao? existingTag = _dbContext.Tags.FirstOrDefault(t => t.Name == tag.Name.Trim());

        if (existingTag == null) return;

        ItemDao? existingItem = _dbContext.Items
            .Include(x => x.Tags)
            .FirstOrDefault(t => t.Name == tagAssignation.Item);
        if (existingItem == null) return;

        existingItem.Tags.Remove(existingTag);
        existingTag.Items.Remove(existingItem);
        ItemTagDao? existingItemTag = _dbContext.ItemTags.FirstOrDefault(x => x.Item.ItemId == existingItem.ItemId && x.Tag.TagId == existingTag.TagId);
        if (existingItemTag != null)
        {
            _dbContext.ItemTags.Remove(existingItemTag);
        }
        
        for (int i = 0; i < existingItem.ItemTags.OrderBy(x => x.OrderIndex).ToList().Count; i++)
        {
            existingItem.ItemTags[i].OrderIndex = i;
        }

        tagAssignation.RemoveTag(_tagCache.GetOrCreate(existingTag));
    }

    public void ToggleToItem(string tagName, CanHaveTag tagAssignation)
    {
        ToggleToItem(new Tag(tagName.Trim()), tagAssignation);
    }

    public void ToggleToItem(Tag tag, CanHaveTag tagAssignation)
    {
        TagDao? existingTag = _dbContext.Tags
            .Include(x => x.Items)
            .Include(x => x.ItemTags)
            .FirstOrDefault(t => t.Name == tag.Name.Trim());
        if (existingTag == null) return;

        ItemDao? existingItem = _dbContext.Items
            .Include(x => x.Tags)
            .Include(x => x.ItemTags)
            .FirstOrDefault(t => t.Name == tagAssignation.Item);
        if (existingItem == null) return;

        ItemTagDao? existingItemTag = _dbContext.ItemTags.FirstOrDefault(t => t.Item.Name == tagAssignation.Item && t.Tag.Name == tag.Name.Trim());

        if (existingItemTag != null)
        {
            existingItem.Tags.Remove(existingTag);
            existingTag.Items.Remove(existingItem);
            existingItem.ItemTags.Remove(existingItemTag);
            existingTag.ItemTags.Remove(existingItemTag);
            _dbContext.ItemTags.Remove(existingItemTag);
            for (int i = 0; i < existingItem.ItemTags.OrderBy(x => x.OrderIndex).ToList().Count; i++)
            {
                existingItem.ItemTags[i].OrderIndex = i;
            }

            tagAssignation.RemoveTag(_tagCache.GetOrCreate(existingTag));
        }
        else
        {
            ItemTagDao newItemTag = new()
            {
                ItemForeignKey = existingItem.ItemId,
                TagForeignKey = existingTag.TagId,
                Tag = existingTag,
                Item = existingItem,
                OrderIndex = existingItem.Tags.Count
            };
            existingItem.Tags.Add(existingTag);
            existingTag.Items.Add(existingItem);
            existingItem.ItemTags.Add(newItemTag);
            existingTag.ItemTags.Add(newItemTag);
            _dbContext.ItemTags.Add(newItemTag);
            tagAssignation.AddTag(_tagCache.GetOrCreate(existingTag));
        }
    }

    public void MarkDone(CanHaveTag tagAssignation)
    {
        ItemDao? existingItem = _dbContext.Items
            .FirstOrDefault(t => t.Name == tagAssignation.Item);

        if (existingItem == null)
        {
            return;
        }

        existingItem.Done = true;

        tagAssignation.Done = true;
    }

    public void DeleteTag(Tag tag)
    {
        TagDao? existingTag = _dbContext.Tags
            .Include(x => x.Items)
            .Include(x => x.ItemTags)
            .FirstOrDefault(t => t.Name == tag.Name.Trim());

        if (existingTag == null)
        {
            return;
        }

        foreach (ItemDao existingItem in existingTag.Items)
        {
            existingItem.Tags.Remove(existingTag);
        }

        foreach (ItemTagDao itemTag in existingTag.ItemTags)
        {
            _dbContext.ItemTags.Remove(itemTag);
        }

        _dbContext.Tags.Remove(existingTag);
    }

    public ImmutableList<string> GetItemsWithTag(Tag tag)
    {
        TagDao? existingTag = _dbContext.Tags
            .Include(x => x.Items)
            .FirstOrDefault(t => t.Name == tag.Name.Trim());

        return existingTag == null ? ImmutableList<string>.Empty : existingTag.Items.Select(x => x.Name).ToImmutableList();
    }
}
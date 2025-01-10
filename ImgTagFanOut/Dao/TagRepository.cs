using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
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

        if (!string.IsNullOrWhiteSpace(newTagLabel) && _dbContext.Tags.FirstOrDefault(x => x.Name == newTagLabel) == null)
        {
            TagDao tagDao = new(newTagLabel);
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

    public ImmutableList<Tag> GetAllTagForHash(string hash)
    {
        ImmutableList<Tag> tags = _dbContext
            .ItemTags.Include(x => x.Item)
            .Include(x => x.Tag)
            .Where(t => t.Item.Hash == hash)
            .Select(t => t.Tag)
            .Distinct()
            .Select(x => _tagCache.GetOrCreate(x))
            .ToImmutableList();
        return tags;
    }

    public async Task AddOrUpdateItem(CanHaveTag tagAssignation, Func<CanHaveTag, Task<string>> getHash)
    {
        ItemDao? existingItem = await _dbContext.Items
            .Include(i => i.ItemTags)
            .ThenInclude(x => x.Tag)
            .FirstOrDefaultAsync(t => t.Name == tagAssignation.Item);
        if (existingItem != null)
        {
            if (!string.IsNullOrEmpty(existingItem.Hash))
            {
                string hash = await getHash(tagAssignation);
                if (existingItem.Hash == hash)
                {
                    foreach (ItemTagDao itemTagDao in existingItem.ItemTags.OrderBy(x => x.OrderIndex))
                    {
                        tagAssignation.AddTag(_tagCache.GetOrCreate(itemTagDao.Tag));
                    }
                    tagAssignation.Done = existingItem.Done;
                }
                else
                {
                    // the file is not the same, so remove the existing assignations
                    _dbContext.ItemTags.RemoveRange(existingItem.ItemTags);
                    existingItem.ItemTags.Clear();
                    existingItem.Tags.Clear();
                    existingItem.Done = false;
                }
            }
        }
        else
        {
            _dbContext.Items.Add(new(tagAssignation.Item));
        }
    }

    public void AddTagToItem(Tag tag, CanHaveTag tagAssignation)
    {
        TagDao? existingTag = _dbContext.Tags.FirstOrDefault(t => t.Name == tag.Name.Trim());
        if (existingTag == null)
            return;

        ItemDao? existingItem = _dbContext.Items.Include(x => x.Tags).FirstOrDefault(t => t.Name == tagAssignation.Item);

        if (existingItem == null)
            return;

        if (tagAssignation.Hash != null)
        {
            existingItem.Hash = tagAssignation.Hash;
        }

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
            new()
            {
                ItemForeignKey = existingItem.ItemId,
                TagForeignKey = existingTag.TagId,
                Tag = existingTag,
                Item = existingItem,
                OrderIndex = existingItem.Tags.Count - 1,
            }
        );
        tagAssignation.AddTag(_tagCache.GetOrCreate(existingTag));
    }

    public void RemoveTagToItem(Tag tag, CanHaveTag tagAssignation)
    {
        TagDao? existingTag = _dbContext.Tags.FirstOrDefault(t => t.Name == tag.Name.Trim());

        if (existingTag == null)
            return;

        ItemDao? existingItem = _dbContext.Items.Include(x => x.Tags).FirstOrDefault(t => t.Name == tagAssignation.Item);

        if (existingItem == null)
            return;

        if (tagAssignation.Hash != null)
        {
            existingItem.Hash = tagAssignation.Hash;
        }

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
        TagDao? existingTag = _dbContext.Tags.Include(x => x.Items).Include(x => x.ItemTags).FirstOrDefault(t => t.Name == tag.Name.Trim());

        if (existingTag == null)
            return;

        ItemDao? existingItem = _dbContext.Items.Include(x => x.Tags).Include(x => x.ItemTags).FirstOrDefault(t => t.Name == tagAssignation.Item);

        if (existingItem == null)
            return;

        if (tagAssignation.Hash != null)
        {
            existingItem.Hash = tagAssignation.Hash;
        }

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
                OrderIndex = existingItem.Tags.Count,
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
        ItemDao? existingItem = _dbContext.Items.FirstOrDefault(t => t.Name == tagAssignation.Item);

        if (existingItem == null)
        {
            return;
        }

        if (tagAssignation.Hash != null)
        {
            existingItem.Hash = tagAssignation.Hash;
        }

        existingItem.Done = true;

        tagAssignation.Done = true;
    }

    public void MarkUnDone(CanHaveTag tagAssignation)
    {
        ItemDao? existingItem = _dbContext.Items.FirstOrDefault(t => t.Name == tagAssignation.Item);

        if (existingItem == null)
        {
            return;
        }

        if (tagAssignation.Hash != null)
        {
            existingItem.Hash = tagAssignation.Hash;
        }

        existingItem.Done = false;

        tagAssignation.Done = false;
    }

    public void DeleteTag(Tag tag)
    {
        TagDao? existingTag = _dbContext.Tags.Include(x => x.Items).Include(x => x.ItemTags).FirstOrDefault(t => t.Name == tag.Name.Trim());

        if (existingTag == null)
        {
            return;
        }

        foreach (ItemDao existingItem in existingTag.Items)
        {
            existingItem.Tags.Remove(existingTag);
        }

        _dbContext.ItemTags.RemoveRange(existingTag.ItemTags);

        _dbContext.Tags.Remove(existingTag);
    }

    public ImmutableList<string> GetItemsWithTag(Tag tag)
    {
        TagDao? existingTag = _dbContext.Tags.Include(x => x.Items).FirstOrDefault(t => t.Name == tag.Name.Trim());

        return existingTag == null ? ImmutableList<string>.Empty : existingTag.Items.Select(x => x.Name).ToImmutableList();
    }
}

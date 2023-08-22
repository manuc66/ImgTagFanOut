using System.Collections.Generic;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Dao;

public class TagCache : ITagCache
{
    private readonly HashSet<Tag> _tags = new(Tag.Comparer);
    
    public Tag GetOrCreate(TagDao tagDao)
    {
        Tag probingTag = new(tagDao.Name);
        if (_tags.TryGetValue(probingTag, out Tag? tag))
        {
            return tag;
        }

        _tags.Add(probingTag);
        return probingTag;
    }

    public void Remove(Tag tag)
    {
        _tags.Remove(tag);
    }
}
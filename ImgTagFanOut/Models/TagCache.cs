using System.Collections.Generic;
using ImgTagFanOut.Dao;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Models;

public class TagCache : ITagCache
{
    private readonly HashSet<Tag> _tags = new(Tag.Comparer);
    private readonly object _sync = new();

    public Tag GetOrCreate(TagDao tagDao)
    {
        Tag probingTag = new(tagDao.Name);
        lock (_sync)
        {
            if (_tags.TryGetValue(probingTag, out Tag? tag))
            {
                return tag;
            }

            _tags.Add(probingTag);
            return probingTag;
        }
    }

    public void Remove(Tag tag)
    {
        lock (_sync)
        {
            _tags.Remove(tag);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _tags.Clear();
        }
    }
}

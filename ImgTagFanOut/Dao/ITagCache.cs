using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut.Dao;

public interface ITagCache
{
    Tag GetOrCreate(TagDao tagDao);
    void Remove(Tag tag);
}

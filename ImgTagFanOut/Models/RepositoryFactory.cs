using ImgTagFanOut.Dao;

namespace ImgTagFanOut.Models;

public class RepositoryFactory
{
    
    private static readonly TagCache _tagCache = _tagCache = new TagCache();
    
    internal static ImgTagFanOutDbContext GetRepo(out ITagRepository repo, string? path = null)
    {
        ImgTagFanOutDbContext imgTagFanOutDbContext = new(path);
        imgTagFanOutDbContext.Database.EnsureCreated();
        repo = new TagRepository(imgTagFanOutDbContext, _tagCache);
        return imgTagFanOutDbContext;
    }

    public static void ClearTagCache()
    {
        _tagCache.Clear();
    }
}
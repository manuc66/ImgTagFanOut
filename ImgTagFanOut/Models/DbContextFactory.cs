using System.Threading.Tasks;
using ImgTagFanOut.Dao;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Models;

public class DbContextFactory
{
    private static readonly TagCache TagCache = TagCache = new TagCache();
    
    internal static async Task<IUnitOfWork> GetUnitOfWorkAsync(string path)
    {
        ImgTagFanOutDbContext imgTagFanOutDbContext = new(TagCache, path);
        //await imgTagFanOutDbContext.Database.EnsureCreatedAsync();
        await imgTagFanOutDbContext.Database.MigrateAsync();
        return imgTagFanOutDbContext;
    }
    internal static IUnitOfWork GetUnitOfWork(string path)
    {
        ImgTagFanOutDbContext imgTagFanOutDbContext = new(TagCache, path);
        //imgTagFanOutDbContext.Database.EnsureCreated();
        imgTagFanOutDbContext.Database.Migrate();
        return imgTagFanOutDbContext;
    }

    public static void ClearTagCache()
    {
        TagCache.Clear();
    }
}
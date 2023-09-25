using System.Threading.Tasks;
using ImgTagFanOut.Dao;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Models;

public static class DbContextFactory
{
    private static readonly TagCache TagCache = TagCache = new();
    
    internal static async Task<IUnitOfWork> GetUnitOfWorkAsync(string path)
    {
        ImgTagFanOutDbContext imgTagFanOutDbContext = new(TagCache, path);
        await imgTagFanOutDbContext.Database.MigrateAsync();
        return imgTagFanOutDbContext;
    }
    internal static IUnitOfWork GetUnitOfWork(string path)
    {
        ImgTagFanOutDbContext imgTagFanOutDbContext = new(TagCache, path);
        
        imgTagFanOutDbContext.Database.Migrate();
        
        return imgTagFanOutDbContext;
    }

    public static void ClearTagCache()
    {
        TagCache.Clear();
    }
}
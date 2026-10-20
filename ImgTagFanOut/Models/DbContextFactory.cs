using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImgTagFanOut.Dao;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Models;

public interface IUnitOfWorkFactory
{
    Task<IUnitOfWork> GetUnitOfWorkAsync(string path, CancellationToken cancellationToken = default);
    IUnitOfWork GetUnitOfWork(string path);
    void ClearTagCache();
    bool DatabaseExists(string path);
    void DeleteDatabase(string path);
}

public static class DbContextFactory
{
    private static readonly TagCache TagCache = TagCache = new();

    internal static async Task<IUnitOfWork> GetUnitOfWorkAsync(string path, CancellationToken cancellationToken = default)
    {
        ImgTagFanOutDbContext imgTagFanOutDbContext = new(TagCache, path);
        await imgTagFanOutDbContext.Database.MigrateAsync(cancellationToken);
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

    public static string GetDatabasePath(string folder)
    {
        return Path.Join(folder, ImgTagFanOutDbContext.DatabaseFileName);
    }

    public static IEnumerable<string> GetDatabaseFilePaths(string folder)
    {
        string databasePath = GetDatabasePath(folder);

        yield return databasePath;
        yield return $"{databasePath}-wal";
        yield return $"{databasePath}-shm";
        yield return $"{databasePath}-journal";
    }

    public static bool DatabaseExists(string folder)
    {
        return GetDatabaseFilePaths(folder).Any(File.Exists);
    }

    /// <summary>
    /// Removes the database and its SQLite sidecar files from a folder. All pooled SQLite
    /// connections are released first so the files can be deleted on every platform.
    /// </summary>
    public static void DeleteDatabase(string folder)
    {
        ClearTagCache();
        SqliteConnection.ClearAllPools();

        foreach (string filePath in GetDatabaseFilePaths(folder))
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}

public sealed class UnitOfWorkFactory : IUnitOfWorkFactory
{
    public Task<IUnitOfWork> GetUnitOfWorkAsync(string path, CancellationToken cancellationToken = default)
    {
        return DbContextFactory.GetUnitOfWorkAsync(path, cancellationToken);
    }

    public IUnitOfWork GetUnitOfWork(string path)
    {
        return DbContextFactory.GetUnitOfWork(path);
    }

    public void ClearTagCache()
    {
        DbContextFactory.ClearTagCache();
    }

    public bool DatabaseExists(string path)
    {
        return DbContextFactory.DatabaseExists(path);
    }

    public void DeleteDatabase(string path)
    {
        DbContextFactory.DeleteDatabase(path);
    }
}

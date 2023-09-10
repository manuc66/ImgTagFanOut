using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Dao;

public interface IImgTagFanOutDbContext
{
    DbSet<TagDao> Tags { get; }
    DbSet<ItemDao> Items { get; }
    DbSet<ItemTagDao> ItemTags { get; }
}

public class ImgTagFanOutDbContext : DbContext, IImgTagFanOutDbContext
{
    private string DbPath { get; }

    public DbSet<TagDao> Tags => Set<TagDao>();
    public DbSet<ItemDao> Items => Set<ItemDao>();
    public DbSet<ItemTagDao> ItemTags => Set<ItemTagDao>();

    public ImgTagFanOutDbContext() : this(".")
    {
    }

    public ImgTagFanOutDbContext(string? folder = ".")
    {
        DbPath = System.IO.Path.Join(folder ?? ".", "ImgTagFanOut.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}")
    //  .LogTo(Console.WriteLine)
    ;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ItemDao>()
            .HasIndex(b => new { b.Name })
            .IsUnique()
            .IsDescending();


        modelBuilder.Entity<ItemDao>().HasKey(x => x.ItemId);
        modelBuilder.Entity<ItemDao>().Property(x => x.ItemId)
            .IsRequired()
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<TagDao>()
            .HasIndex(b => new { b.Name })
            .IsUnique()
            .IsDescending();

        modelBuilder.Entity<TagDao>().HasKey(x => x.TagId);
        modelBuilder.Entity<TagDao>().Property(x => x.TagId)
            .IsRequired()
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<ItemTagDao>()
            .HasIndex(b => new { b.OrderIndex })
            .IsDescending();

        modelBuilder.Entity<ItemDao>()
            .HasMany(e => e.Tags)
            .WithMany(e => e.Items)
            .UsingEntity<ItemTagDao>(
                l => l.HasOne<TagDao>(e => e.Tag).WithMany(e => e.ItemTags).HasForeignKey(e => e.TagForeignKey),
                r => r.HasOne<ItemDao>(e => e.Item).WithMany(e => e.ItemTags).HasForeignKey(e => e.ItemForeignKey));
    }
}
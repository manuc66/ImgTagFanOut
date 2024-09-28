using ImgTagFanOut.Models;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Dao;

public class ImgTagFanOutDbContext : DbContext, IImgTagFanOutDbContext, IUnitOfWork
{
    private string DbPath { get; }

    public DbSet<TagDao> Tags { get; set; } = null!;
    public DbSet<ItemDao> Items { get; set; } = null!;
    public DbSet<ItemTagDao> ItemTags { get; set; } = null!;
    public DbSet<ParameterDao> Parameters { get; set; } = null!;
    public ITagRepository TagRepository { get; }
    public IParameterRepository ParameterRepository { get; }

    public ImgTagFanOutDbContext() : this(new(), ".")
    {
    }

    public ImgTagFanOutDbContext(TagCache tagCache, string folder)
    {
        DbPath = System.IO.Path.Join(folder, "ImgTagFanOut.db");
        TagRepository = new TagRepository(this, tagCache);
        ParameterRepository = new ParameterRepository(this);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}")
    //  .LogTo(Console.WriteLine)
    ;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Items
        modelBuilder.Entity<ItemDao>()
            .ToTable("items");

        modelBuilder.Entity<ItemDao>()
            .HasIndex(b => new { b.Name })
            .IsUnique()
            .IsDescending();

        modelBuilder.Entity<ItemDao>()
            .HasIndex(b => new { b.Hash });

        modelBuilder.Entity<ItemDao>().HasKey(x => x.ItemId);
        modelBuilder.Entity<ItemDao>().Property(x => x.ItemId)
            .IsRequired()
            .ValueGeneratedOnAdd();

        // Tags
        modelBuilder.Entity<TagDao>()
            .ToTable("tags");

        modelBuilder.Entity<TagDao>()
            .HasIndex(b => new { b.Name })
            .IsUnique()
            .IsDescending();

        modelBuilder.Entity<TagDao>().HasKey(x => x.TagId);
        modelBuilder.Entity<TagDao>().Property(x => x.TagId)
            .IsRequired()
            .ValueGeneratedOnAdd();

        // ItemTags
        modelBuilder.Entity<ItemTagDao>()
            .ToTable("item_tags");

        modelBuilder.Entity<ItemTagDao>()
            .HasIndex(b => new { b.OrderIndex })
            .IsDescending();

        modelBuilder.Entity<ItemDao>()
            .HasMany(e => e.Tags)
            .WithMany(e => e.Items)
            .UsingEntity<ItemTagDao>(
                l => l.HasOne<TagDao>(e => e.Tag).WithMany(e => e.ItemTags).HasForeignKey(e => e.TagForeignKey),
                r => r.HasOne<ItemDao>(e => e.Item).WithMany(e => e.ItemTags).HasForeignKey(e => e.ItemForeignKey));

        // Parameters
        modelBuilder.Entity<ParameterDao>()
            .ToTable("parameters");

        modelBuilder.Entity<ParameterDao>()
            .HasIndex(b => new { b.Name })
            .IsUnique()
            .IsDescending();
        modelBuilder.Entity<ParameterDao>().HasKey(x => x.ParameterId);
        modelBuilder.Entity<ParameterDao>().Property(x => x.ParameterId)
            .IsRequired()
            .ValueGeneratedOnAdd();
    }
}

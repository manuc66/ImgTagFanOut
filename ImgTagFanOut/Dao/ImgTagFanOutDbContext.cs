using ImgTagFanOut.Models;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Dao;

public class ImgTagFanOutDbContext : DbContext, IImgTagFanOutDbContext, IUnitOfWork
{
    public const string DatabaseFileName = "ImgTagFanOut.db";

    private readonly string? DbPath;

    public DbSet<TagDao> Tags { get; set; } = null!;
    public DbSet<ItemDao> Items { get; set; } = null!;
    public DbSet<ItemTagDao> ItemTags { get; set; } = null!;
    public DbSet<ParameterDao> Parameters { get; set; } = null!;
    public DbSet<FaceDao> Faces { get; set; } = null!;
    public DbSet<FaceAnalysisDao> FaceAnalyses { get; set; } = null!;
    public DbSet<FaceTagDao> FaceTags { get; set; } = null!;
    public ITagRepository TagRepository { get; }
    public IParameterRepository ParameterRepository { get; }
    public IFaceRepository FaceRepository { get; }

    public ImgTagFanOutDbContext()
        : this(new(), ".") { }

    public ImgTagFanOutDbContext(TagCache tagCache, string folder)
    {
        DbPath = System.IO.Path.Join(folder, DatabaseFileName);
        TagRepository = new TagRepository(this, tagCache);
        ParameterRepository = new ParameterRepository(this);
        FaceRepository = new FaceRepository(this);
    }

    public ImgTagFanOutDbContext(DbContextOptions<ImgTagFanOutDbContext> options, TagCache tagCache)
        : base(options)
    {
        TagRepository = new TagRepository(this, tagCache);
        ParameterRepository = new ParameterRepository(this);
        FaceRepository = new FaceRepository(this);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite($"Data Source={DbPath ?? System.IO.Path.Join(".", DatabaseFileName)}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Items
        modelBuilder.Entity<ItemDao>().ToTable("items");

        modelBuilder.Entity<ItemDao>().HasIndex(b => new { b.Name }).IsUnique().IsDescending();

        modelBuilder.Entity<ItemDao>().HasIndex(b => new { b.Hash });

        modelBuilder.Entity<ItemDao>().HasKey(x => x.ItemId);
        modelBuilder.Entity<ItemDao>().Property(x => x.ItemId).IsRequired().ValueGeneratedOnAdd();

        // Tags
        modelBuilder.Entity<TagDao>().ToTable("tags");

        modelBuilder.Entity<TagDao>().HasIndex(b => new { b.Name }).IsUnique().IsDescending();

        modelBuilder.Entity<TagDao>().HasKey(x => x.TagId);
        modelBuilder.Entity<TagDao>().Property(x => x.TagId).IsRequired().ValueGeneratedOnAdd();

        // ItemTags
        modelBuilder.Entity<ItemTagDao>().ToTable("item_tags");

        modelBuilder.Entity<ItemTagDao>().HasIndex(b => new { b.OrderIndex }).IsDescending();

        modelBuilder
            .Entity<ItemDao>()
            .HasMany(e => e.Tags)
            .WithMany(e => e.Items)
            .UsingEntity<ItemTagDao>(
                l => l.HasOne<TagDao>(e => e.Tag).WithMany(e => e.ItemTags).HasForeignKey(e => e.TagForeignKey),
                r => r.HasOne<ItemDao>(e => e.Item).WithMany(e => e.ItemTags).HasForeignKey(e => e.ItemForeignKey)
            );

        // Parameters
        modelBuilder.Entity<ParameterDao>().ToTable("parameters");

        modelBuilder.Entity<ParameterDao>().HasIndex(b => new { b.Name }).IsUnique().IsDescending();
        modelBuilder.Entity<ParameterDao>().HasKey(x => x.ParameterId);
        modelBuilder.Entity<ParameterDao>().Property(x => x.ParameterId).IsRequired().ValueGeneratedOnAdd();

        // Faces
        modelBuilder.Entity<FaceDao>().ToTable("faces");

        modelBuilder.Entity<FaceDao>().HasKey(x => x.FaceId);
        modelBuilder.Entity<FaceDao>().Property(x => x.FaceId).IsRequired().ValueGeneratedOnAdd();
        modelBuilder.Entity<FaceDao>().HasIndex(x => new { x.ItemForeignKey });

        modelBuilder
            .Entity<FaceDao>()
            .HasOne(x => x.Item)
            .WithMany(x => x.Faces)
            .HasForeignKey(x => x.ItemForeignKey)
            .OnDelete(DeleteBehavior.Cascade);

        // FaceAnalyses
        modelBuilder.Entity<FaceAnalysisDao>().ToTable("face_analyses");

        modelBuilder.Entity<FaceAnalysisDao>().HasKey(x => x.ItemForeignKey);
        modelBuilder.Entity<FaceAnalysisDao>().Property(x => x.ItemForeignKey).IsRequired();
        modelBuilder.Entity<FaceAnalysisDao>().HasIndex(x => new { x.SourceHash });

        modelBuilder
            .Entity<FaceAnalysisDao>()
            .HasOne(x => x.Item)
            .WithOne(x => x.FaceAnalysis)
            .HasForeignKey<FaceAnalysisDao>(x => x.ItemForeignKey)
            .OnDelete(DeleteBehavior.Cascade);

        // FaceTags
        modelBuilder.Entity<FaceTagDao>().ToTable("face_tags");

        modelBuilder.Entity<FaceTagDao>().HasKey(x => new { x.FaceId, x.TagId });
        modelBuilder.Entity<FaceTagDao>().HasIndex(x => new { x.TagId });

        modelBuilder
            .Entity<FaceTagDao>()
            .HasOne(x => x.Face)
            .WithMany(x => x.FaceTags)
            .HasForeignKey(x => x.FaceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Entity<FaceTagDao>()
            .HasOne(x => x.Tag)
            .WithMany(x => x.FaceTags)
            .HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

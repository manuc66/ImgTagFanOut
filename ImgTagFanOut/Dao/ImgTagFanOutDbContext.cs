using System;
using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Dao;

public class ImgTagFanOutDbContext : DbContext
{
    public string DbPath { get; }
    
    public ImgTagFanOutDbContext():this(".")
    {
    }
    public ImgTagFanOutDbContext(string? folder = ".")
    {
        DbPath = System.IO.Path.Join(folder ?? ".", "default-folder.db");
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ItemDao>()
            .HasIndex(b => new { b.Name})
            .IsUnique()
            .IsDescending();
        
        modelBuilder.Entity<ItemDao>().HasKey(x => x.ItemId);
        modelBuilder.Entity<ItemDao>().Property(x => x.ItemId)
            .IsRequired()
            .ValueGeneratedOnAdd();
        
        modelBuilder.Entity<TagDao>()
            .HasIndex(b => new { b.Name})
            .IsUnique()
            .IsDescending();
        
        modelBuilder.Entity<TagDao>().HasKey(x => x.TagId);
        modelBuilder.Entity<TagDao>().Property(x => x.TagId)
            .IsRequired()
            .ValueGeneratedOnAdd();
        
        modelBuilder.Entity<ItemTagDao>()
            .HasIndex(b => new { b.OrderIndex})
            .IsUnique()
            .IsDescending();
        
        modelBuilder.Entity<ItemDao>()
            .HasMany(e => e.Tags)
            .WithMany(e => e.Items)
            .UsingEntity<ItemTagDao>(
                l => l.HasOne<TagDao>(e => e.Tag).WithMany(e => e.ItemTags).HasForeignKey(e => e.TagForeignKey),
                r => r.HasOne<ItemDao>(e => e.Item).WithMany(e => e.ItemTags).HasForeignKey(e => e.ItemForeignKey));
    }
}
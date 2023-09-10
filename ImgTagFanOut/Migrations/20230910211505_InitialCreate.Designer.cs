﻿// <auto-generated />
using ImgTagFanOut.Dao;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace ImgTagFanOut.Migrations
{
    [DbContext(typeof(ImgTagFanOutDbContext))]
    [Migration("20230910211505_InitialCreate")]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.10");

            modelBuilder.Entity("ImgTagFanOut.Dao.ItemDao", b =>
                {
                    b.Property<int>("ItemId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Done")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("ItemId");

                    b.HasIndex("Name")
                        .IsUnique()
                        .IsDescending();

                    b.ToTable("items", (string)null);
                });

            modelBuilder.Entity("ImgTagFanOut.Dao.ItemTagDao", b =>
                {
                    b.Property<int>("ItemForeignKey")
                        .HasColumnType("INTEGER");

                    b.Property<int>("TagForeignKey")
                        .HasColumnType("INTEGER");

                    b.Property<int>("OrderIndex")
                        .HasColumnType("INTEGER");

                    b.HasKey("ItemForeignKey", "TagForeignKey");

                    b.HasIndex("OrderIndex")
                        .IsDescending();

                    b.HasIndex("TagForeignKey");

                    b.ToTable("item_tags", (string)null);
                });

            modelBuilder.Entity("ImgTagFanOut.Dao.ParameterDao", b =>
                {
                    b.Property<int>("ParameterId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Value")
                        .HasColumnType("TEXT");

                    b.HasKey("ParameterId");

                    b.HasIndex("Name")
                        .IsUnique()
                        .IsDescending();

                    b.ToTable("parameters", (string)null);
                });

            modelBuilder.Entity("ImgTagFanOut.Dao.TagDao", b =>
                {
                    b.Property<int>("TagId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("TagId");

                    b.HasIndex("Name")
                        .IsUnique()
                        .IsDescending();

                    b.ToTable("tags", (string)null);
                });

            modelBuilder.Entity("ImgTagFanOut.Dao.ItemTagDao", b =>
                {
                    b.HasOne("ImgTagFanOut.Dao.ItemDao", "Item")
                        .WithMany("ItemTags")
                        .HasForeignKey("ItemForeignKey")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ImgTagFanOut.Dao.TagDao", "Tag")
                        .WithMany("ItemTags")
                        .HasForeignKey("TagForeignKey")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Item");

                    b.Navigation("Tag");
                });

            modelBuilder.Entity("ImgTagFanOut.Dao.ItemDao", b =>
                {
                    b.Navigation("ItemTags");
                });

            modelBuilder.Entity("ImgTagFanOut.Dao.TagDao", b =>
                {
                    b.Navigation("ItemTags");
                });
#pragma warning restore 612, 618
        }
    }
}
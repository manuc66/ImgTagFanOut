using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImgTagFanOut.Migrations
{
    /// <inheritdoc />
    public partial class AddTagSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "item_embeddings",
                columns: table => new
                {
                    ItemForeignKey = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceHash = table.Column<string>(type: "TEXT", nullable: false),
                    ModelVersion = table.Column<string>(type: "TEXT", nullable: false),
                    Dimensions = table.Column<int>(type: "INTEGER", nullable: false),
                    Embedding = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_embeddings", x => x.ItemForeignKey);
                    table.ForeignKey(
                        name: "FK_item_embeddings_items_ItemForeignKey",
                        column: x => x.ItemForeignKey,
                        principalTable: "items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tag_suggestions",
                columns: table => new
                {
                    ItemForeignKey = table.Column<int>(type: "INTEGER", nullable: false),
                    TagName = table.Column<string>(type: "TEXT", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    ReasonArgument = table.Column<string>(type: "TEXT", nullable: false),
                    SourceHash = table.Column<string>(type: "TEXT", nullable: false),
                    EngineVersion = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    SuggestedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tag_suggestions", x => new { x.ItemForeignKey, x.TagName });
                    table.ForeignKey(
                        name: "FK_tag_suggestions_items_ItemForeignKey",
                        column: x => x.ItemForeignKey,
                        principalTable: "items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_item_embeddings_SourceHash",
                table: "item_embeddings",
                column: "SourceHash");

            migrationBuilder.CreateIndex(
                name: "IX_tag_suggestions_SourceHash",
                table: "tag_suggestions",
                column: "SourceHash");

            migrationBuilder.CreateIndex(
                name: "IX_tag_suggestions_TagName_State",
                table: "tag_suggestions",
                columns: new[] { "TagName", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_embeddings");

            migrationBuilder.DropTable(
                name: "tag_suggestions");
        }
    }
}

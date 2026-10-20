using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImgTagFanOut.Migrations
{
    /// <inheritdoc />
    public partial class AddFaceAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "face_analyses",
                columns: table => new
                {
                    ItemForeignKey = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceHash = table.Column<string>(type: "TEXT", nullable: false),
                    DetectorVersion = table.Column<string>(type: "TEXT", nullable: false),
                    FaceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AnalyzedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_face_analyses", x => x.ItemForeignKey);
                    table.ForeignKey(
                        name: "FK_face_analyses_items_ItemForeignKey",
                        column: x => x.ItemForeignKey,
                        principalTable: "items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "faces",
                columns: table => new
                {
                    FaceId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemForeignKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    X = table.Column<float>(type: "REAL", nullable: false),
                    Y = table.Column<float>(type: "REAL", nullable: false),
                    Width = table.Column<float>(type: "REAL", nullable: false),
                    Height = table.Column<float>(type: "REAL", nullable: false),
                    Landmarks = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Score = table.Column<float>(type: "REAL", nullable: false),
                    Embedding = table.Column<byte[]>(type: "BLOB", nullable: true),
                    EmbedderVersion = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faces", x => x.FaceId);
                    table.ForeignKey(
                        name: "FK_faces_items_ItemForeignKey",
                        column: x => x.ItemForeignKey,
                        principalTable: "items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_face_analyses_SourceHash",
                table: "face_analyses",
                column: "SourceHash");

            migrationBuilder.CreateIndex(
                name: "IX_faces_ItemForeignKey",
                table: "faces",
                column: "ItemForeignKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "face_analyses");

            migrationBuilder.DropTable(
                name: "faces");
        }
    }
}

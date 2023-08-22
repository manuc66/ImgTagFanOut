using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImgTagFanOut.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItemDao",
                columns: table => new
                {
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemDao", x => x.ItemId);
                });

            migrationBuilder.CreateTable(
                name: "TagDao",
                columns: table => new
                {
                    TagId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagDao", x => x.TagId);
                });

            migrationBuilder.CreateTable(
                name: "ItemTagDao",
                columns: table => new
                {
                    ItemForeignKey = table.Column<int>(type: "INTEGER", nullable: false),
                    TagForeignKey = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderIndex = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemTagDao", x => new { x.ItemForeignKey, x.TagForeignKey });
                    table.ForeignKey(
                        name: "FK_ItemTagDao_ItemDao_ItemForeignKey",
                        column: x => x.ItemForeignKey,
                        principalTable: "ItemDao",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemTagDao_TagDao_TagForeignKey",
                        column: x => x.TagForeignKey,
                        principalTable: "TagDao",
                        principalColumn: "TagId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemDao_Name",
                table: "ItemDao",
                column: "Name",
                unique: true,
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ItemTagDao_OrderIndex",
                table: "ItemTagDao",
                column: "OrderIndex",
                unique: true,
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ItemTagDao_TagForeignKey",
                table: "ItemTagDao",
                column: "TagForeignKey");

            migrationBuilder.CreateIndex(
                name: "IX_TagDao_Name",
                table: "TagDao",
                column: "Name",
                unique: true,
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemTagDao");

            migrationBuilder.DropTable(
                name: "ItemDao");

            migrationBuilder.DropTable(
                name: "TagDao");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImgTagFanOut.Migrations
{
    /// <inheritdoc />
    public partial class AddHashOnItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Hash", table: "items", type: "TEXT", nullable: true);

            migrationBuilder.CreateIndex(name: "IX_items_Hash", table: "items", column: "Hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_items_Hash", table: "items");

            migrationBuilder.DropColumn(name: "Hash", table: "items");
        }
    }
}

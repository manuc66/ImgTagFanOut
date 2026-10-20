using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImgTagFanOut.Migrations
{
    /// <inheritdoc />
    public partial class AddFaceTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "face_tags",
                columns: table => new
                {
                    FaceId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_face_tags", x => new { x.FaceId, x.TagId });
                    table.ForeignKey(
                        name: "FK_face_tags_faces_FaceId",
                        column: x => x.FaceId,
                        principalTable: "faces",
                        principalColumn: "FaceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_face_tags_tags_TagId",
                        column: x => x.TagId,
                        principalTable: "tags",
                        principalColumn: "TagId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_face_tags_TagId",
                table: "face_tags",
                column: "TagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "face_tags");
        }
    }
}

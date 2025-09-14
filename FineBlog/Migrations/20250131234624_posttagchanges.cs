using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FineBlog.Migrations
{
    /// <inheritdoc />
    public partial class posttagchanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PostTags_Posts_PostsId",
                table: "PostTags");

            migrationBuilder.DropForeignKey(
                name: "FK_PostTags_Tags_TagsId",
                table: "PostTags");

            migrationBuilder.RenameColumn(
                name: "TagsId",
                table: "PostTags",
                newName: "TagId");

            migrationBuilder.RenameColumn(
                name: "PostsId",
                table: "PostTags",
                newName: "PostId");

            migrationBuilder.RenameIndex(
                name: "IX_PostTags_TagsId",
                table: "PostTags",
                newName: "IX_PostTags_TagId");

            migrationBuilder.AddForeignKey(
                name: "FK_PostTags_Posts_PostId",
                table: "PostTags",
                column: "PostId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PostTags_Tags_TagId",
                table: "PostTags",
                column: "TagId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PostTags_Posts_PostId",
                table: "PostTags");

            migrationBuilder.DropForeignKey(
                name: "FK_PostTags_Tags_TagId",
                table: "PostTags");

            migrationBuilder.RenameColumn(
                name: "TagId",
                table: "PostTags",
                newName: "TagsId");

            migrationBuilder.RenameColumn(
                name: "PostId",
                table: "PostTags",
                newName: "PostsId");

            migrationBuilder.RenameIndex(
                name: "IX_PostTags_TagId",
                table: "PostTags",
                newName: "IX_PostTags_TagsId");

            migrationBuilder.AddForeignKey(
                name: "FK_PostTags_Posts_PostsId",
                table: "PostTags",
                column: "PostsId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PostTags_Tags_TagsId",
                table: "PostTags",
                column: "TagsId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

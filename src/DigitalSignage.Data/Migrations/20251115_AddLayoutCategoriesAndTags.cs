using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalSignage.Data.Migrations
{
    /// <summary>
    /// Migration to add Category and Tags fields to DisplayLayout
    /// </summary>
    public partial class AddLayoutCategoriesAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "DisplayLayouts",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "DisplayLayouts",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateIndex(
                name: "IX_DisplayLayouts_Category",
                table: "DisplayLayouts",
                column: "Category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DisplayLayouts_Category",
                table: "DisplayLayouts");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "DisplayLayouts");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "DisplayLayouts");
        }
    }
}

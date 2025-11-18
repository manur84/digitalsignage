using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalSignage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingDisplayLayoutProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "DisplayLayouts",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PngContentBase64",
                table: "DisplayLayouts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LayoutType",
                table: "DisplayLayouts",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileName",
                table: "DisplayLayouts");

            migrationBuilder.DropColumn(
                name: "PngContentBase64",
                table: "DisplayLayouts");

            migrationBuilder.DropColumn(
                name: "LayoutType",
                table: "DisplayLayouts");
        }
    }
}

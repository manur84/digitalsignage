using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalSignage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkedDataSourceIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LinkedDataSourceIds",
                table: "DisplayLayouts",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LinkedDataSourceIds",
                table: "DisplayLayouts");
        }
    }
}

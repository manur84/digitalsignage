using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalSignage.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLayoutTemplateEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LayoutTemplates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LayoutTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    ThumbnailPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    BackgroundColor = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    BackgroundImage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ElementsJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UsageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Resolution = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LayoutTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LayoutTemplates_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LayoutTemplates_Category",
                table: "LayoutTemplates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_LayoutTemplates_CreatedAt",
                table: "LayoutTemplates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LayoutTemplates_CreatedByUserId",
                table: "LayoutTemplates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LayoutTemplates_IsBuiltIn",
                table: "LayoutTemplates",
                column: "IsBuiltIn");

            migrationBuilder.CreateIndex(
                name: "IX_LayoutTemplates_Name",
                table: "LayoutTemplates",
                column: "Name");
        }
    }
}

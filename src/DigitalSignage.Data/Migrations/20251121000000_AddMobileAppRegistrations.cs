using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalSignage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileAppRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobileAppRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AppVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Permissions = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AuthorizedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AuthorizedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileAppRegistrations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobileAppRegistrations_DeviceIdentifier",
                table: "MobileAppRegistrations",
                column: "DeviceIdentifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobileAppRegistrations_Token",
                table: "MobileAppRegistrations",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobileAppRegistrations_Status",
                table: "MobileAppRegistrations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MobileAppRegistrations_RegisteredAt",
                table: "MobileAppRegistrations",
                column: "RegisteredAt");

            migrationBuilder.CreateIndex(
                name: "IX_MobileAppRegistrations_LastSeenAt",
                table: "MobileAppRegistrations",
                column: "LastSeenAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MobileAppRegistrations");
        }
    }
}

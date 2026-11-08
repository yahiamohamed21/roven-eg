using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddHeroBannersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HeroBanners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DesktopImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MobileImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RedirectLink = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AltText = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeroBanners", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HeroBanners_DisplayOrder",
                table: "HeroBanners",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_HeroBanners_IsActive",
                table: "HeroBanners",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HeroBanners");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddMainBrandHeroCustomization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrandHeroAltText",
                table: "HomeSettings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "ROVEN FRAGRANCES — Your Safest Choice • Explore the Collection");

            migrationBuilder.AddColumn<string>(
                name: "BrandHeroDesktopImageUrl",
                table: "HomeSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "/images/banners/roven-hero-main.png");

            migrationBuilder.AddColumn<string>(
                name: "BrandHeroMobileImageUrl",
                table: "HomeSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "/images/banners/roven-hero-main-sm.png");

            migrationBuilder.AddColumn<string>(
                name: "BrandHeroRedirectLink",
                table: "HomeSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "/Store");

            migrationBuilder.AddColumn<string>(
                name: "BrandHeroTitle",
                table: "HomeSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "ROVEN FRAGRANCES — Your Safest Choice");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrandHeroAltText",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "BrandHeroDesktopImageUrl",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "BrandHeroMobileImageUrl",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "BrandHeroRedirectLink",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "BrandHeroTitle",
                table: "HomeSettings");
        }
    }
}

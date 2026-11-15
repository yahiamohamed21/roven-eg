using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddFeaturedProductsCustomization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FeaturedBadgeText",
                table: "HomeSettings",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FeaturedExploreText",
                table: "HomeSettings",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FeaturedProductIds",
                table: "HomeSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeaturedSubtitle",
                table: "HomeSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FeaturedTitle",
                table: "HomeSettings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeaturedBadgeText",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "FeaturedExploreText",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "FeaturedProductIds",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "FeaturedSubtitle",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "FeaturedTitle",
                table: "HomeSettings");
        }
    }
}

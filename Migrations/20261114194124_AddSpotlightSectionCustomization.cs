using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddSpotlightSectionCustomization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpotlightBadgeText",
                table: "HomeSettings",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SpotlightSubtitle",
                table: "HomeSettings",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SpotlightTitle",
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
                name: "SpotlightBadgeText",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "SpotlightSubtitle",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "SpotlightTitle",
                table: "HomeSettings");
        }
    }
}

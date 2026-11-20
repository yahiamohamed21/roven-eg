using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomFieldsToHomeOffer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ButtonText",
                table: "HomeOffers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomDescription",
                table: "HomeOffers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomImageUrl",
                table: "HomeOffers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeaturePills",
                table: "HomeOffers",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPriceOverride",
                table: "HomeOffers",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ButtonText",
                table: "HomeOffers");

            migrationBuilder.DropColumn(
                name: "CustomDescription",
                table: "HomeOffers");

            migrationBuilder.DropColumn(
                name: "CustomImageUrl",
                table: "HomeOffers");

            migrationBuilder.DropColumn(
                name: "FeaturePills",
                table: "HomeOffers");

            migrationBuilder.DropColumn(
                name: "OriginalPriceOverride",
                table: "HomeOffers");
        }
    }
}

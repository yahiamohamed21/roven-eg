using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddBestSellersCustomization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBestSeller",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BestSellersBadgeText",
                table: "HomeSettings",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "Most Requested");

            migrationBuilder.AddColumn<string>(
                name: "BestSellersProductIds",
                table: "HomeSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BestSellersSubtitle",
                table: "HomeSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "The fragrances most cherished by our clientele across Egypt this month");

            migrationBuilder.AddColumn<string>(
                name: "BestSellersTitle",
                table: "HomeSettings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "Top Acclaimed ROVEN Parfums");

            migrationBuilder.Sql(@"
UPDATE HomeSettings 
SET BestSellersBadgeText = 'Most Requested' 
WHERE BestSellersBadgeText IS NULL OR BestSellersBadgeText = '';

UPDATE HomeSettings 
SET BestSellersTitle = 'Top Acclaimed ROVEN Parfums' 
WHERE BestSellersTitle IS NULL OR BestSellersTitle = '';

UPDATE HomeSettings 
SET BestSellersSubtitle = 'The fragrances most cherished by our clientele across Egypt this month' 
WHERE BestSellersSubtitle IS NULL OR BestSellersSubtitle = '';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBestSeller",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BestSellersBadgeText",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "BestSellersProductIds",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "BestSellersSubtitle",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "BestSellersTitle",
                table: "HomeSettings");
        }
    }
}

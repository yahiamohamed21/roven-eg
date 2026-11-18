using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddShowOnHomeToProductReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowOnHome",
                table: "ProductReviews",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_ShowOnHome",
                table: "ProductReviews",
                column: "ShowOnHome");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductReviews_ShowOnHome",
                table: "ProductReviews");

            migrationBuilder.DropColumn(
                name: "ShowOnHome",
                table: "ProductReviews");
        }
    }
}

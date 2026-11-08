using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddSecondProductToHomeOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HomeOffers_Products_ProductId",
                table: "HomeOffers");

            migrationBuilder.AddColumn<int>(
                name: "SecondProductId",
                table: "HomeOffers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "HomeOffers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeOffers_DisplayOrder",
                table: "HomeOffers",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_HomeOffers_IsActive",
                table: "HomeOffers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_HomeOffers_SecondProductId",
                table: "HomeOffers",
                column: "SecondProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_HomeOffers_Products_ProductId",
                table: "HomeOffers",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HomeOffers_Products_SecondProductId",
                table: "HomeOffers",
                column: "SecondProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HomeOffers_Products_ProductId",
                table: "HomeOffers");

            migrationBuilder.DropForeignKey(
                name: "FK_HomeOffers_Products_SecondProductId",
                table: "HomeOffers");

            migrationBuilder.DropIndex(
                name: "IX_HomeOffers_DisplayOrder",
                table: "HomeOffers");

            migrationBuilder.DropIndex(
                name: "IX_HomeOffers_IsActive",
                table: "HomeOffers");

            migrationBuilder.DropIndex(
                name: "IX_HomeOffers_SecondProductId",
                table: "HomeOffers");

            migrationBuilder.DropColumn(
                name: "SecondProductId",
                table: "HomeOffers");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "HomeOffers");

            migrationBuilder.AddForeignKey(
                name: "FK_HomeOffers_Products_ProductId",
                table: "HomeOffers",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

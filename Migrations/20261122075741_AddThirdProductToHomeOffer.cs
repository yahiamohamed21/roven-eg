using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddThirdProductToHomeOffer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ThirdProductId",
                table: "HomeOffers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeOffers_ThirdProductId",
                table: "HomeOffers",
                column: "ThirdProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_HomeOffers_Products_ThirdProductId",
                table: "HomeOffers",
                column: "ThirdProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HomeOffers_Products_ThirdProductId",
                table: "HomeOffers");

            migrationBuilder.DropIndex(
                name: "IX_HomeOffers_ThirdProductId",
                table: "HomeOffers");

            migrationBuilder.DropColumn(
                name: "ThirdProductId",
                table: "HomeOffers");
        }
    }
}

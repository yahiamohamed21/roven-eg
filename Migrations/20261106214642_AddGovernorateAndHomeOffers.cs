using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddGovernorateAndHomeOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ShippingFee",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingFee",
                table: "AbandonedCheckouts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Governorates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ShippingFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DeliveryTime = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Governorates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HomeOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    BadgeText = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OfferPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AllocatedPercent = table.Column<int>(type: "int", nullable: false),
                    RemainingCount = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeOffers_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HomeOffers_ProductId",
                table: "HomeOffers",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Governorates");

            migrationBuilder.DropTable(
                name: "HomeOffers");

            migrationBuilder.DropColumn(
                name: "ShippingFee",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingFee",
                table: "AbandonedCheckouts");
        }
    }
}

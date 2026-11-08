using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddEcommerceStorefrontSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerEmail",
                table: "Orders",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "Orders",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerPhone",
                table: "Orders",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderNotes",
                table: "Orders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderSource",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Cash on Delivery");

            migrationBuilder.AddColumn<string>(
                name: "ShippingAddress",
                table: "Orders",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderId",
                table: "InventoryTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_OrderId",
                table: "InventoryTransactions",
                column: "OrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_Orders_OrderId",
                table: "InventoryTransactions",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_Orders_OrderId",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_OrderId",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerEmail",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerPhone",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderNotes",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderSource",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingAddress",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "InventoryTransactions");
        }
    }
}

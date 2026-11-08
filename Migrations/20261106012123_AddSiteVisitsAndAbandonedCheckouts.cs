using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteVisitsAndAbandonedCheckouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AbandonedCheckouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerPhone = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CustomerEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShippingAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CartItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ItemCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRecovered = table.Column<bool>(type: "bit", nullable: false),
                    RecoveredOrderId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbandonedCheckouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbandonedCheckouts_Orders_RecoveredOrderId",
                        column: x => x.RecoveredOrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SiteVisits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Path = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Referer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeviceType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VisitedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteVisits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbandonedCheckouts_CustomerPhone",
                table: "AbandonedCheckouts",
                column: "CustomerPhone");

            migrationBuilder.CreateIndex(
                name: "IX_AbandonedCheckouts_IsRecovered",
                table: "AbandonedCheckouts",
                column: "IsRecovered");

            migrationBuilder.CreateIndex(
                name: "IX_AbandonedCheckouts_RecoveredOrderId",
                table: "AbandonedCheckouts",
                column: "RecoveredOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_SessionId",
                table: "SiteVisits",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_VisitedAt",
                table: "SiteVisits",
                column: "VisitedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbandonedCheckouts");

            migrationBuilder.DropTable(
                name: "SiteVisits");
        }
    }
}

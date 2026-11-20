using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddFragranceDiscoverySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FragranceProfileReviewedAt",
                table: "Products",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspiredBy",
                table: "Products",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFragranceProfileApproved",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Projection",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FragranceNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ArabicName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FragranceNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductSeasons",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Season = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSeasons", x => new { x.ProductId, x.Season });
                    table.ForeignKey(
                        name: "FK_ProductSeasons_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductTargetAudiences",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Audience = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTargetAudiences", x => new { x.ProductId, x.Audience });
                    table.ForeignKey(
                        name: "FK_ProductTargetAudiences_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FragranceNoteQuickPicks",
                columns: table => new
                {
                    FragranceNoteId = table.Column<int>(type: "int", nullable: false),
                    Context = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FragranceNoteQuickPicks", x => new { x.FragranceNoteId, x.Context });
                    table.ForeignKey(
                        name: "FK_FragranceNoteQuickPicks_FragranceNotes_FragranceNoteId",
                        column: x => x.FragranceNoteId,
                        principalTable: "FragranceNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductFragranceNotes",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    FragranceNoteId = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductFragranceNotes", x => new { x.ProductId, x.FragranceNoteId });
                    table.ForeignKey(
                        name: "FK_ProductFragranceNotes_FragranceNotes_FragranceNoteId",
                        column: x => x.FragranceNoteId,
                        principalTable: "FragranceNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductFragranceNotes_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsFragranceProfileApproved",
                table: "Products",
                column: "IsFragranceProfileApproved");

            migrationBuilder.CreateIndex(
                name: "IX_FragranceNoteQuickPicks_Context",
                table: "FragranceNoteQuickPicks",
                column: "Context");

            migrationBuilder.CreateIndex(
                name: "IX_FragranceNoteQuickPicks_DisplayOrder",
                table: "FragranceNoteQuickPicks",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_FragranceNotes_Category",
                table: "FragranceNotes",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_FragranceNotes_DisplayOrder",
                table: "FragranceNotes",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_FragranceNotes_IsActive",
                table: "FragranceNotes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FragranceNotes_Name",
                table: "FragranceNotes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductFragranceNotes_FragranceNoteId",
                table: "ProductFragranceNotes",
                column: "FragranceNoteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FragranceNoteQuickPicks");

            migrationBuilder.DropTable(
                name: "ProductFragranceNotes");

            migrationBuilder.DropTable(
                name: "ProductSeasons");

            migrationBuilder.DropTable(
                name: "ProductTargetAudiences");

            migrationBuilder.DropTable(
                name: "FragranceNotes");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsFragranceProfileApproved",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "FragranceProfileReviewedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "InspiredBy",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsFragranceProfileApproved",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Projection",
                table: "Products");
        }
    }
}

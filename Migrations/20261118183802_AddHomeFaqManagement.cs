using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeFaqManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaqBadgeText",
                table: "HomeSettings",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "Maison ROVEN Clarifications");

            migrationBuilder.AddColumn<string>(
                name: "FaqSubtitle",
                table: "HomeSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "Everything you need to know about concentrations, parcel inspection, and authenticity");

            migrationBuilder.AddColumn<string>(
                name: "FaqTitle",
                table: "HomeSettings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "Frequently Asked Questions");

            migrationBuilder.CreateTable(
                name: "HomeFaqItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Question = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IconClass = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeFaqItems", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HomeFaqItems");

            migrationBuilder.DropColumn(
                name: "FaqBadgeText",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "FaqSubtitle",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "FaqTitle",
                table: "HomeSettings");
        }
    }
}

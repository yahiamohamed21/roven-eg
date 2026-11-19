using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddComparisonSectionToHomeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ComparisonActive",
                table: "HomeSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ComparisonAfterImageUrl",
                table: "HomeSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComparisonAfterLabel",
                table: "HomeSettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ComparisonBadgeText",
                table: "HomeSettings",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ComparisonBeforeImageUrl",
                table: "HomeSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComparisonBeforeLabel",
                table: "HomeSettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ComparisonButtonLink",
                table: "HomeSettings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ComparisonButtonText",
                table: "HomeSettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ComparisonSubtitle",
                table: "HomeSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ComparisonTitle",
                table: "HomeSettings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "OrderComparison",
                table: "HomeSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ShowComparisonSection",
                table: "HomeSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComparisonActive",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "ComparisonAfterImageUrl",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "ComparisonAfterLabel",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "ComparisonBadgeText",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "ComparisonBeforeImageUrl",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "ComparisonBeforeLabel",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "ComparisonButtonLink",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "ComparisonButtonText",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "ComparisonSubtitle",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "ComparisonTitle",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "OrderComparison",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "ShowComparisonSection",
                table: "HomeSettings");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddHomepageSectionOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrderBestSellers",
                table: "HomeSettings",
                type: "int",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.AddColumn<int>(
                name: "OrderBrandHero",
                table: "HomeSettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "OrderCategories",
                table: "HomeSettings",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "OrderFaq",
                table: "HomeSettings",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "OrderFeatured",
                table: "HomeSettings",
                type: "int",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<int>(
                name: "OrderHeroBanners",
                table: "HomeSettings",
                type: "int",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<int>(
                name: "OrderMarquee",
                table: "HomeSettings",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "OrderNewsletter",
                table: "HomeSettings",
                type: "int",
                nullable: false,
                defaultValue: 11);

            migrationBuilder.AddColumn<int>(
                name: "OrderOffers",
                table: "HomeSettings",
                type: "int",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<int>(
                name: "OrderTestimonials",
                table: "HomeSettings",
                type: "int",
                nullable: false,
                defaultValue: 9);

            migrationBuilder.AddColumn<int>(
                name: "OrderTrustBadges",
                table: "HomeSettings",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<bool>(
                name: "ShowBrandHero",
                table: "HomeSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowHeroBanners",
                table: "HomeSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowTestimonialsSection",
                table: "HomeSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "MediaDeletionJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PublicId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    MaxRetries = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRetryUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LockedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaDeletionJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaDeletionJobs_ImageUrl",
                table: "MediaDeletionJobs",
                column: "ImageUrl");

            migrationBuilder.CreateIndex(
                name: "IX_MediaDeletionJobs_Status_NextRetry",
                table: "MediaDeletionJobs",
                columns: new[] { "Status", "NextRetryUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaDeletionJobs");

            migrationBuilder.DropColumn(
                name: "OrderBestSellers",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "OrderBrandHero",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "OrderCategories",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "OrderFaq",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "OrderFeatured",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "OrderHeroBanners",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "OrderMarquee",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "OrderNewsletter",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "OrderOffers",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "OrderTestimonials",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "OrderTrustBadges",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "ShowBrandHero",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "ShowHeroBanners",
                table: "HomeSettings");

            migrationBuilder.DropColumn(
                name: "ShowTestimonialsSection",
                table: "HomeSettings");
        }
    }
}

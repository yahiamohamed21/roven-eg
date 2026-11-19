using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class SetWinterCategoryDefaultImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Categories SET ImageUrl = '/roven/categories/winter.jpg' WHERE (LOWER(Name) LIKE '%winter%' OR Id = 2) AND (ImageUrl IS NULL OR ImageUrl = '');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

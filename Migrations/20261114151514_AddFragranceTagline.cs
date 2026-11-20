using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddFragranceTagline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Products', 'FragranceTagline') IS NULL
    ALTER TABLE Products ADD FragranceTagline NVARCHAR(MAX) NULL;

IF COL_LENGTH('HomeSettings', 'DefaultFragranceTagline') IS NULL
    ALTER TABLE HomeSettings ADD DefaultFragranceTagline NVARCHAR(200) NOT NULL CONSTRAINT DF_HomeSettings_DefaultFragranceTagline DEFAULT 'Extrait de Parfum • 30% Pure Oil';
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('Products', 'FragranceTagline') IS NOT NULL
    EXEC(N'UPDATE Products SET FragranceTagline = ''Extrait de Parfum • 30% Pure Oil'' WHERE FragranceTagline IS NULL;');

IF COL_LENGTH('HomeSettings', 'DefaultFragranceTagline') IS NOT NULL
    EXEC(N'UPDATE HomeSettings SET DefaultFragranceTagline = ''Extrait de Parfum • 30% Pure Oil'' WHERE DefaultFragranceTagline IS NULL OR DefaultFragranceTagline = '''';');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Products', 'FragranceTagline') IS NOT NULL
    ALTER TABLE Products DROP COLUMN FragranceTagline;

IF COL_LENGTH('HomeSettings', 'DefaultFragranceTagline') IS NOT NULL
    ALTER TABLE HomeSettings DROP COLUMN DefaultFragranceTagline;
");
        }
    }
}

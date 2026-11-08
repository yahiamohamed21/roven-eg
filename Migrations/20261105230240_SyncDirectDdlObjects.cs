using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class SyncDirectDdlObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty synchronization migration:
            // The objects scripted in this migration (Coupons, ContactMessages, HomeSettings tables,
            // Orders.CouponId/CouponCode/DiscountAmount columns, and AspNetUsers.Permissions column)
            // were already created in the SQL Server database via direct DDL commands.
            // This migration safely synchronizes ApplicationDbContextModelSnapshot with the database schema
            // and records the migration ID in __EFMigrationsHistory without modifying existing database objects.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

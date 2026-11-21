using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockify.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogSyncTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncChangeFeedEvents",
                columns: table => new
                {
                    Cursor = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntitySyncKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityVersion = table.Column<int>(type: "int", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncChangeFeedEvents", x => x.Cursor);
                });

            migrationBuilder.CreateTable(
                name: "SyncConflicts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntitySyncKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LocalPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LivePayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConflictingFields = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ChosenWinner = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncConflicts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncHistoryLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ItemsProcessed = table.Column<int>(type: "int", nullable: false),
                    ConflictsDetected = table.Column<int>(type: "int", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncHistoryLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncOutboxEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntitySyncKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntityVersion = table.Column<int>(type: "int", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncOutboxEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncStateRecords",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncStateRecords", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncChangeFeedEvents_Cursor",
                table: "SyncChangeFeedEvents",
                column: "Cursor");

            migrationBuilder.CreateIndex(
                name: "IX_SyncChangeFeedEvents_EntityType_EntitySyncKey",
                table: "SyncChangeFeedEvents",
                columns: new[] { "EntityType", "EntitySyncKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncChangeFeedEvents_TimestampUtc",
                table: "SyncChangeFeedEvents",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SyncConflicts_DetectedAtUtc",
                table: "SyncConflicts",
                column: "DetectedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SyncConflicts_EntityType_EntitySyncKey",
                table: "SyncConflicts",
                columns: new[] { "EntityType", "EntitySyncKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncConflicts_Status",
                table: "SyncConflicts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SyncOutboxEvents_CreatedAtUtc",
                table: "SyncOutboxEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SyncOutboxEvents_EntityType_EntitySyncKey",
                table: "SyncOutboxEvents",
                columns: new[] { "EntityType", "EntitySyncKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncOutboxEvents_State",
                table: "SyncOutboxEvents",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncChangeFeedEvents");

            migrationBuilder.DropTable(
                name: "SyncConflicts");

            migrationBuilder.DropTable(
                name: "SyncHistoryLogs");

            migrationBuilder.DropTable(
                name: "SyncOutboxEvents");

            migrationBuilder.DropTable(
                name: "SyncStateRecords");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelligentMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExternalMessageId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessingLeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_EventId",
                table: "NotificationDeliveries",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_IdempotencyKey",
                table: "NotificationDeliveries",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_Status_NextAttemptAtUtc",
                table: "NotificationDeliveries",
                columns: new[] { "Status", "NextAttemptAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDeliveries");
        }
    }
}

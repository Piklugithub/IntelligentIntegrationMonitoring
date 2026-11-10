using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelligentMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialMonitoringSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationFailures",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventVersion = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IntegrationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationFailures", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "FailureInvestigations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HasLogs = table.Column<bool>(type: "bit", nullable: false),
                    HasFailures = table.Column<bool>(type: "bit", nullable: false),
                    TotalLogs = table.Column<int>(type: "int", nullable: false),
                    InformationCount = table.Column<int>(type: "int", nullable: false),
                    WarningCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    FirstLogAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastLogAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureTimestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureServiceName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ServicesInvolvedJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimelineJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailureInvestigations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FailureInvestigations_IntegrationFailures_EventId",
                        column: x => x.EventId,
                        principalTable: "IntegrationFailures",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FailureInvestigations_EventId",
                table: "FailureInvestigations",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationFailures_OccurredAtUtc",
                table: "IntegrationFailures",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationFailures_ServiceName",
                table: "IntegrationFailures",
                column: "ServiceName");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationFailures_TraceId",
                table: "IntegrationFailures",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationFailures_TransactionId",
                table: "IntegrationFailures",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FailureInvestigations");

            migrationBuilder.DropTable(
                name: "IntegrationFailures");
        }
    }
}

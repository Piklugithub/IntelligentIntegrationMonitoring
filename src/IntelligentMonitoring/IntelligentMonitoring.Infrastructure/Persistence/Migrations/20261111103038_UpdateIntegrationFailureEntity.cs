using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelligentMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateIntegrationFailureEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAtUtc",
                table: "IntegrationFailures",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvestigationStatus",
                table: "IntegrationFailures",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptAtUtc",
                table: "IntegrationFailures",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "IntegrationFailures",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextRetryAtUtc",
                table: "IntegrationFailures",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "IntegrationFailures",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "IntegrationFailures");

            migrationBuilder.DropColumn(
                name: "InvestigationStatus",
                table: "IntegrationFailures");

            migrationBuilder.DropColumn(
                name: "LastAttemptAtUtc",
                table: "IntegrationFailures");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "IntegrationFailures");

            migrationBuilder.DropColumn(
                name: "NextRetryAtUtc",
                table: "IntegrationFailures");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "IntegrationFailures");
        }
    }
}

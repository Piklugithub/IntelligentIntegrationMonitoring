using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelligentMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingLeaseToIntegrationFailures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessingLeaseExpiresAtUtc",
                table: "IntegrationFailures",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessingLeaseExpiresAtUtc",
                table: "IntegrationFailures");
        }
    }
}

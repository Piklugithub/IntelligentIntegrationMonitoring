using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelligentMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFailureInvestigationEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiAnalysisStatus",
                table: "FailureInvestigations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AiAnalyzedAtUtc",
                table: "FailureInvestigations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiBusinessImpact",
                table: "FailureInvestigations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiFailureStage",
                table: "FailureInvestigations",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiInformationGapsJson",
                table: "FailureInvestigations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AiModelName",
                table: "FailureInvestigations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiProbableRootCause",
                table: "FailureInvestigations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiRecommendedActionsJson",
                table: "FailureInvestigations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AiRootCauseConfidence",
                table: "FailureInvestigations",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiSummary",
                table: "FailureInvestigations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiSupportingEvidenceJson",
                table: "FailureInvestigations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiAnalysisStatus",
                table: "FailureInvestigations");

            migrationBuilder.DropColumn(
                name: "AiAnalyzedAtUtc",
                table: "FailureInvestigations");

            migrationBuilder.DropColumn(
                name: "AiBusinessImpact",
                table: "FailureInvestigations");

            migrationBuilder.DropColumn(
                name: "AiFailureStage",
                table: "FailureInvestigations");

            migrationBuilder.DropColumn(
                name: "AiInformationGapsJson",
                table: "FailureInvestigations");

            migrationBuilder.DropColumn(
                name: "AiModelName",
                table: "FailureInvestigations");

            migrationBuilder.DropColumn(
                name: "AiProbableRootCause",
                table: "FailureInvestigations");

            migrationBuilder.DropColumn(
                name: "AiRecommendedActionsJson",
                table: "FailureInvestigations");

            migrationBuilder.DropColumn(
                name: "AiRootCauseConfidence",
                table: "FailureInvestigations");

            migrationBuilder.DropColumn(
                name: "AiSummary",
                table: "FailureInvestigations");

            migrationBuilder.DropColumn(
                name: "AiSupportingEvidenceJson",
                table: "FailureInvestigations");
        }
    }
}

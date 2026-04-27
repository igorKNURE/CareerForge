using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace CareerForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CacheEmbeddingsAndMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MatchReports_ResumeId_JobDescriptionId",
                table: "MatchReports");

            migrationBuilder.DropIndex(
                name: "IX_MatchReports_UserId",
                table: "MatchReports");

            migrationBuilder.AddColumn<Vector>(
                name: "SummaryEmbedding",
                table: "Resumes",
                type: "vector(768)",
                nullable: true);

            migrationBuilder.AddColumn<Vector>(
                name: "SummaryEmbedding",
                table: "JobDescriptions",
                type: "vector(768)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchReports_ResumeId",
                table: "MatchReports",
                column: "ResumeId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReports_UserId_ResumeId_JobDescriptionId",
                table: "MatchReports",
                columns: new[] { "UserId", "ResumeId", "JobDescriptionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MatchReports_ResumeId",
                table: "MatchReports");

            migrationBuilder.DropIndex(
                name: "IX_MatchReports_UserId_ResumeId_JobDescriptionId",
                table: "MatchReports");

            migrationBuilder.DropColumn(
                name: "SummaryEmbedding",
                table: "Resumes");

            migrationBuilder.DropColumn(
                name: "SummaryEmbedding",
                table: "JobDescriptions");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReports_ResumeId_JobDescriptionId",
                table: "MatchReports",
                columns: new[] { "ResumeId", "JobDescriptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchReports_UserId",
                table: "MatchReports",
                column: "UserId");
        }
    }
}

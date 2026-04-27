using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistMatchSkillLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MatchedMustHaveSkills",
                table: "MatchReports",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MatchedNiceToHaveSkills",
                table: "MatchReports",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MissingMustHaveSkills",
                table: "MatchReports",
                type: "jsonb",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MatchedMustHaveSkills",
                table: "MatchReports");

            migrationBuilder.DropColumn(
                name: "MatchedNiceToHaveSkills",
                table: "MatchReports");

            migrationBuilder.DropColumn(
                name: "MissingMustHaveSkills",
                table: "MatchReports");
        }
    }
}

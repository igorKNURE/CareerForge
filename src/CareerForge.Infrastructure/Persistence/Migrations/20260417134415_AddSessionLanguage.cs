using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "InterviewSessions",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "en");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "InterviewSessions");
        }
    }
}

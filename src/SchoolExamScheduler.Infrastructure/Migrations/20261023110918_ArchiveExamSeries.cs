using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolExamScheduler.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ArchiveExamSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchivedSnapshot",
                table: "Rosters",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedSnapshot",
                table: "Rosters");
        }
    }
}

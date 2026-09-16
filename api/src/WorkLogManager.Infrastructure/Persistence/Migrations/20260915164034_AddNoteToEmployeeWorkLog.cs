using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkLogManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteToEmployeeWorkLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "employee_work_logs",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "note",
                table: "employee_work_logs");
        }
    }
}

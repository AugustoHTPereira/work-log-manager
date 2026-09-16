using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkLogManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginToEmployeeWorkLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "origin",
                table: "employee_work_logs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Manual");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "origin",
                table: "employee_work_logs");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkLogManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthClosing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "month_closing_id",
                table: "employee_work_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "month_closings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_month_closings", x => x.id);
                    table.CheckConstraint("ck_month_closings_month_range", "month >= 1 AND month <= 12");
                });

            migrationBuilder.CreateIndex(
                name: "ix_employee_work_logs_month_closing_id",
                table: "employee_work_logs",
                column: "month_closing_id");

            migrationBuilder.CreateIndex(
                name: "ix_month_closings_year_month",
                table: "month_closings",
                columns: new[] { "year", "month" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_employee_work_logs_month_closings_month_closing_id",
                table: "employee_work_logs",
                column: "month_closing_id",
                principalTable: "month_closings",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_employee_work_logs_month_closings_month_closing_id",
                table: "employee_work_logs");

            migrationBuilder.DropTable(
                name: "month_closings");

            migrationBuilder.DropIndex(
                name: "ix_employee_work_logs_month_closing_id",
                table: "employee_work_logs");

            migrationBuilder.DropColumn(
                name: "month_closing_id",
                table: "employee_work_logs");
        }
    }
}

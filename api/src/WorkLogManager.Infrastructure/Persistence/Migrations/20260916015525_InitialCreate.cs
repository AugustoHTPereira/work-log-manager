using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkLogManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    hire_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employees", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "month_closings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    month = table.Column<int>(type: "INTEGER", nullable: false),
                    year = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_month_closings", x => x.id);
                    table.CheckConstraint("ck_month_closings_month_range", "month >= 1 AND month <= 12");
                });

            migrationBuilder.CreateTable(
                name: "system_parameters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    param = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    value_type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_parameters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_schedule_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    employee_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    day_of_week = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_schedule_periods", x => x.id);
                    table.CheckConstraint("ck_work_schedule_periods_end_after_start", "end_time > start_time");
                    table.ForeignKey(
                        name: "fk_work_schedule_periods_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_work_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    employee_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    start_date = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    end_date = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    duration_seconds = table.Column<long>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    month_closing_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    note = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    origin = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_work_logs", x => x.id);
                    table.CheckConstraint("ck_employee_work_logs_end_date_after_start_date", "end_date >= start_date");
                    table.ForeignKey(
                        name: "fk_employee_work_logs_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_employee_work_logs_month_closings_month_closing_id",
                        column: x => x.month_closing_id,
                        principalTable: "month_closings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employee_work_logs_employee_id",
                table: "employee_work_logs",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_work_logs_month_closing_id",
                table: "employee_work_logs",
                column: "month_closing_id");

            migrationBuilder.CreateIndex(
                name: "ix_month_closings_year_month",
                table: "month_closings",
                columns: new[] { "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_system_parameters_param_value",
                table: "system_parameters",
                columns: new[] { "param", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_schedule_periods_employee_id_day_of_week",
                table: "work_schedule_periods",
                columns: new[] { "employee_id", "day_of_week" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_work_logs");

            migrationBuilder.DropTable(
                name: "system_parameters");

            migrationBuilder.DropTable(
                name: "work_schedule_periods");

            migrationBuilder.DropTable(
                name: "month_closings");

            migrationBuilder.DropTable(
                name: "employees");
        }
    }
}

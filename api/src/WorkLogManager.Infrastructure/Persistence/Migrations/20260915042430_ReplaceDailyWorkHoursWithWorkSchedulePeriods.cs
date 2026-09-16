using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkLogManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceDailyWorkHoursWithWorkSchedulePeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_employees_daily_work_hours_positive",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "daily_work_hours",
                table: "employees");

            migrationBuilder.CreateTable(
                name: "work_schedule_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    day_of_week = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "ix_work_schedule_periods_employee_id_day_of_week",
                table: "work_schedule_periods",
                columns: new[] { "employee_id", "day_of_week" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_schedule_periods");

            migrationBuilder.AddColumn<decimal>(
                name: "daily_work_hours",
                table: "employees",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_daily_work_hours = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 8.00m),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_settings", x => x.id);
                    table.CheckConstraint("ck_system_settings_default_daily_work_hours_positive", "default_daily_work_hours > 0");
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "default_daily_work_hours", "updated_at_utc" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), 8.00m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.AddCheckConstraint(
                name: "ck_employees_daily_work_hours_positive",
                table: "employees",
                sql: "daily_work_hours IS NULL OR daily_work_hours > 0");
        }
    }
}

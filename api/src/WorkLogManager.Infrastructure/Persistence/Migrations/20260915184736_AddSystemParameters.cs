using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkLogManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_parameters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    param = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    value_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_parameters", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_system_parameters_param_value",
                table: "system_parameters",
                columns: new[] { "param", "value" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_parameters");
        }
    }
}

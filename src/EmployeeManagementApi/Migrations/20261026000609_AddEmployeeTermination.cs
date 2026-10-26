using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmployeeManagementApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeTermination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TerminationDate",
                table: "Employees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminationReason",
                table: "Employees",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // Los empleados ya inactivos no tenían fecha de baja: se usa la de su última modificación.
            migrationBuilder.Sql(@"
                UPDATE Employees
                SET TerminationDate = CAST(COALESCE(UpdatedAt, CreatedAt) AS date),
                    TerminationReason = 'Other'
                WHERE IsActive = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TerminationDate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "TerminationReason",
                table: "Employees");
        }
    }
}

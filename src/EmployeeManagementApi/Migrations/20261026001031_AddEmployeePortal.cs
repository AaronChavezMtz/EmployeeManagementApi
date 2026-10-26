using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmployeeManagementApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeePortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmployeeId",
                table: "Users",
                column: "EmployeeId",
                unique: true,
                filter: "[EmployeeId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Employees_EmployeeId",
                table: "Users",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // El rol Viewer (solo lectura) desaparece: esas cuentas pasan a ser del portal del empleado.
            migrationBuilder.Sql("UPDATE Users SET Role = 'Employee' WHERE Role = 'Viewer';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Users SET Role = 'Viewer' WHERE Role = 'Employee';");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Employees_EmployeeId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_EmployeeId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Users");
        }
    }
}

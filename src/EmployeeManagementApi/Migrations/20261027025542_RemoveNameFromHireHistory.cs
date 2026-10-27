using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmployeeManagementApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNameFromHireHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los textos de contratación guardaban el nombre del momento ("Ana García contratado como X");
            // si la ficha se renombraba, el portal mostraba un nombre ajeno. Se deja solo "Contratado como X".
            // Cubre "contratado" y "contratada". 17 = longitud de ' contratado como ' (LEN() ignora el espacio final).
            migrationBuilder.Sql(@"
                UPDATE EmployeeHistories
                SET NewValue = 'Contratado como ' + SUBSTRING(NewValue, PATINDEX('% contratad[oa] como %', NewValue) + 17, 500)
                WHERE ChangeType = 'Created' AND PATINDEX('% contratad[oa] como %', NewValue) > 0;");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

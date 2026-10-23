using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFacturacion.Data.Migrations
{
    public partial class AgregarVersionConcurrenciaProductos : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server asigna automáticamente rowversion a cada producto.
            // También lo actualiza con cualquier cambio de stock o datos.
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Productos",
                type: "rowversion",
                rowVersion: true,
                nullable: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Productos");
        }
    }
}

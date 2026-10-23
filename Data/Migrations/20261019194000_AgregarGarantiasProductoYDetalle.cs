using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFacturacion.Data.Migrations
{
    public partial class AgregarGarantiasProductoYDetalle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 0 indica que en la aplicación no se registró un plazo
            // de garantía comercial. No altera facturas anteriores.
            migrationBuilder.AddColumn<int>(
                name: "GarantiaMeses",
                table: "Productos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GarantiaMeses",
                table: "DetallesFactura",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GarantiaMeses",
                table: "DetallesFactura");

            migrationBuilder.DropColumn(
                name: "GarantiaMeses",
                table: "Productos");
        }
    }
}

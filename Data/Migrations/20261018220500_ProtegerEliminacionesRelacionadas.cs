using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFacturacion.Data.Migrations
{
    public partial class ProtegerEliminacionesRelacionadas : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se conserva la relación, pero se impide el borrado en cascada.
            migrationBuilder.DropForeignKey(
                name: "FK_Facturas_Clientes_IdCliente",
                table: "Facturas");

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Categorias_IdCategoria",
                table: "Productos");

            migrationBuilder.DropForeignKey(
                name: "FK_DetallesFactura_Productos_IdProducto",
                table: "DetallesFactura");

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_Clientes_IdCliente",
                table: "Facturas",
                column: "IdCliente",
                principalTable: "Clientes",
                principalColumn: "IdCliente",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Categorias_IdCategoria",
                table: "Productos",
                column: "IdCategoria",
                principalTable: "Categorias",
                principalColumn: "IdCategoria",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesFactura_Productos_IdProducto",
                table: "DetallesFactura",
                column: "IdProducto",
                principalTable: "Productos",
                principalColumn: "IdProducto",
                onDelete: ReferentialAction.NoAction);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Facturas_Clientes_IdCliente",
                table: "Facturas");

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Categorias_IdCategoria",
                table: "Productos");

            migrationBuilder.DropForeignKey(
                name: "FK_DetallesFactura_Productos_IdProducto",
                table: "DetallesFactura");

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_Clientes_IdCliente",
                table: "Facturas",
                column: "IdCliente",
                principalTable: "Clientes",
                principalColumn: "IdCliente",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Categorias_IdCategoria",
                table: "Productos",
                column: "IdCategoria",
                principalTable: "Categorias",
                principalColumn: "IdCategoria",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesFactura_Productos_IdProducto",
                table: "DetallesFactura",
                column: "IdProducto",
                principalTable: "Productos",
                principalColumn: "IdProducto",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

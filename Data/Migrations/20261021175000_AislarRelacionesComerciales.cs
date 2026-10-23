using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFacturacion.Data.Migrations
{
    /// <summary>
    /// Fase SaaS 1B-2C: evita referencias entre empresas diferentes.
    /// Conserva datos, claves primarias y defaults temporales de la empresa inicial.
    /// Todavía NO autoriza registro multiempresa ni despliegues públicos.
    /// </summary>
    public partial class AislarRelacionesComerciales : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IMPORTANTE: no corregir registros automáticamente. Si existe
            // una referencia cruzada, abortar y restaurar desde el respaldo
            // o revisar los datos manualmente antes de continuar.

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM [dbo].[Productos] AS hija
    INNER JOIN [dbo].[Categorias] AS padre ON hija.[IdCategoria] = padre.[IdCategoria]
    WHERE hija.[IdEmpresa] <> padre.[IdEmpresa])
    THROW 51030, 'Relacion comercial de diferente empresa: Productos -> Categorias', 1;
");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM [dbo].[Facturas] AS hija
    INNER JOIN [dbo].[Clientes] AS padre ON hija.[IdCliente] = padre.[IdCliente]
    WHERE hija.[IdEmpresa] <> padre.[IdEmpresa])
    THROW 51031, 'Relacion comercial de diferente empresa: Facturas -> Clientes', 1;
");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM [dbo].[DetallesFactura] AS hija
    INNER JOIN [dbo].[Facturas] AS padre ON hija.[IdFactura] = padre.[IdFactura]
    WHERE hija.[IdEmpresa] <> padre.[IdEmpresa])
    THROW 51032, 'Relacion comercial de diferente empresa: DetallesFactura -> Facturas', 1;
");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM [dbo].[DetallesFactura] AS hija
    INNER JOIN [dbo].[Productos] AS padre ON hija.[IdProducto] = padre.[IdProducto]
    WHERE hija.[IdEmpresa] <> padre.[IdEmpresa])
    THROW 51033, 'Relacion comercial de diferente empresa: DetallesFactura -> Productos', 1;
");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM [dbo].[Pagos] AS hija
    INNER JOIN [dbo].[Facturas] AS padre ON hija.[IdFactura] = padre.[IdFactura]
    WHERE hija.[IdEmpresa] <> padre.[IdEmpresa])
    THROW 51034, 'Relacion comercial de diferente empresa: Pagos -> Facturas', 1;
");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM [dbo].[CasosPosventa] AS hija
    INNER JOIN [dbo].[DetallesFactura] AS padre ON hija.[IdDetalle] = padre.[IdDetalle]
    WHERE hija.[IdEmpresa] <> padre.[IdEmpresa])
    THROW 51035, 'Relacion comercial de diferente empresa: CasosPosventa -> DetallesFactura', 1;
");

            // Los FKs antiguos apuntan a PK global. Las claves alternas
            // permiten apuntar a (IdEmpresa, IdRegistro) sin alterar PK.

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Categorias_IdCategoria",
                table: "Productos");

            migrationBuilder.DropForeignKey(
                name: "FK_Facturas_Clientes_IdCliente",
                table: "Facturas");

            migrationBuilder.DropForeignKey(
                name: "FK_DetallesFactura_Facturas_IdFactura",
                table: "DetallesFactura");

            migrationBuilder.DropForeignKey(
                name: "FK_DetallesFactura_Productos_IdProducto",
                table: "DetallesFactura");

            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_Facturas_IdFactura",
                table: "Pagos");

            migrationBuilder.DropForeignKey(
                name: "FK_CasosPosventa_DetallesFactura_IdDetalle",
                table: "CasosPosventa");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Categorias_IdEmpresa_IdCategoria",
                table: "Categorias",
                columns: new[] { "IdEmpresa", "IdCategoria" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Clientes_IdEmpresa_IdCliente",
                table: "Clientes",
                columns: new[] { "IdEmpresa", "IdCliente" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Productos_IdEmpresa_IdProducto",
                table: "Productos",
                columns: new[] { "IdEmpresa", "IdProducto" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Facturas_IdEmpresa_IdFactura",
                table: "Facturas",
                columns: new[] { "IdEmpresa", "IdFactura" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_DetallesFactura_IdEmpresa_IdDetalle",
                table: "DetallesFactura",
                columns: new[] { "IdEmpresa", "IdDetalle" });

            migrationBuilder.CreateIndex(
                name: "IX_Productos_IdEmpresa_IdCategoria",
                table: "Productos",
                columns: new[] { "IdEmpresa", "IdCategoria" });

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_IdEmpresa_IdCliente",
                table: "Facturas",
                columns: new[] { "IdEmpresa", "IdCliente" });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesFactura_IdEmpresa_IdFactura",
                table: "DetallesFactura",
                columns: new[] { "IdEmpresa", "IdFactura" });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesFactura_IdEmpresa_IdProducto",
                table: "DetallesFactura",
                columns: new[] { "IdEmpresa", "IdProducto" });

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_IdEmpresa_IdFactura",
                table: "Pagos",
                columns: new[] { "IdEmpresa", "IdFactura" });

            migrationBuilder.CreateIndex(
                name: "IX_CasosPosventa_IdEmpresa_IdDetalle",
                table: "CasosPosventa",
                columns: new[] { "IdEmpresa", "IdDetalle" });

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Categorias_IdEmpresa_IdCategoria",
                table: "Productos",
                columns: new[] { "IdEmpresa", "IdCategoria" },
                principalTable: "Categorias",
                principalColumns: new[] { "IdEmpresa", "IdCategoria" },
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_Clientes_IdEmpresa_IdCliente",
                table: "Facturas",
                columns: new[] { "IdEmpresa", "IdCliente" },
                principalTable: "Clientes",
                principalColumns: new[] { "IdEmpresa", "IdCliente" },
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesFactura_Facturas_IdEmpresa_IdFactura",
                table: "DetallesFactura",
                columns: new[] { "IdEmpresa", "IdFactura" },
                principalTable: "Facturas",
                principalColumns: new[] { "IdEmpresa", "IdFactura" },
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesFactura_Productos_IdEmpresa_IdProducto",
                table: "DetallesFactura",
                columns: new[] { "IdEmpresa", "IdProducto" },
                principalTable: "Productos",
                principalColumns: new[] { "IdEmpresa", "IdProducto" },
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_Facturas_IdEmpresa_IdFactura",
                table: "Pagos",
                columns: new[] { "IdEmpresa", "IdFactura" },
                principalTable: "Facturas",
                principalColumns: new[] { "IdEmpresa", "IdFactura" },
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_CasosPosventa_DetallesFactura_IdEmpresa_IdDetalle",
                table: "CasosPosventa",
                columns: new[] { "IdEmpresa", "IdDetalle" },
                principalTable: "DetallesFactura",
                principalColumns: new[] { "IdEmpresa", "IdDetalle" },
                onDelete: ReferentialAction.NoAction);

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversión estructural; no elimina datos comerciales.

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Categorias_IdEmpresa_IdCategoria",
                table: "Productos");

            migrationBuilder.DropForeignKey(
                name: "FK_Facturas_Clientes_IdEmpresa_IdCliente",
                table: "Facturas");

            migrationBuilder.DropForeignKey(
                name: "FK_DetallesFactura_Facturas_IdEmpresa_IdFactura",
                table: "DetallesFactura");

            migrationBuilder.DropForeignKey(
                name: "FK_DetallesFactura_Productos_IdEmpresa_IdProducto",
                table: "DetallesFactura");

            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_Facturas_IdEmpresa_IdFactura",
                table: "Pagos");

            migrationBuilder.DropForeignKey(
                name: "FK_CasosPosventa_DetallesFactura_IdEmpresa_IdDetalle",
                table: "CasosPosventa");

            migrationBuilder.DropIndex(
                name: "IX_Productos_IdEmpresa_IdCategoria",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Facturas_IdEmpresa_IdCliente",
                table: "Facturas");

            migrationBuilder.DropIndex(
                name: "IX_DetallesFactura_IdEmpresa_IdFactura",
                table: "DetallesFactura");

            migrationBuilder.DropIndex(
                name: "IX_DetallesFactura_IdEmpresa_IdProducto",
                table: "DetallesFactura");

            migrationBuilder.DropIndex(
                name: "IX_Pagos_IdEmpresa_IdFactura",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "IX_CasosPosventa_IdEmpresa_IdDetalle",
                table: "CasosPosventa");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Categorias_IdEmpresa_IdCategoria",
                table: "Categorias");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Clientes_IdEmpresa_IdCliente",
                table: "Clientes");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Productos_IdEmpresa_IdProducto",
                table: "Productos");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Facturas_IdEmpresa_IdFactura",
                table: "Facturas");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_DetallesFactura_IdEmpresa_IdDetalle",
                table: "DetallesFactura");

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Categorias_IdCategoria",
                table: "Productos",
                column: "IdCategoria",
                principalTable: "Categorias",
                principalColumn: "IdCategoria",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_Clientes_IdCliente",
                table: "Facturas",
                column: "IdCliente",
                principalTable: "Clientes",
                principalColumn: "IdCliente",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesFactura_Facturas_IdFactura",
                table: "DetallesFactura",
                column: "IdFactura",
                principalTable: "Facturas",
                principalColumn: "IdFactura",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesFactura_Productos_IdProducto",
                table: "DetallesFactura",
                column: "IdProducto",
                principalTable: "Productos",
                principalColumn: "IdProducto",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_Facturas_IdFactura",
                table: "Pagos",
                column: "IdFactura",
                principalTable: "Facturas",
                principalColumn: "IdFactura",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CasosPosventa_DetallesFactura_IdDetalle",
                table: "CasosPosventa",
                column: "IdDetalle",
                principalTable: "DetallesFactura",
                principalColumn: "IdDetalle",
                onDelete: ReferentialAction.NoAction);

        }
    }
}

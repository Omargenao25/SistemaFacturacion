using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFacturacion.Data.Migrations
{
    /// <summary>
    /// Fase SaaS 1B-1: migración compatible con el mononegocio actual.
    /// NO habilita el aislamiento de usuarios. Los defaults temporales se
    /// retirarán antes del alta de una segunda empresa.
    /// </summary>
    public partial class PrepararDatosComercialesPorEmpresa : Migration
    {
        private static readonly Guid EmpresaLegada =
            new("09d17ec1-956c-4c6e-85b3-317c7cce4e01");

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Falla de forma segura si ya se creó otra empresa en esta base.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [Empresas]
           WHERE [IdEmpresa] <> '09d17ec1-956c-4c6e-85b3-317c7cce4e01')
    THROW 51001, 'La base contiene otras empresas. No aplicar backfill mononegocio; revisar migracion manualmente.', 1;

IF EXISTS (SELECT 1 FROM [Empresas]
           WHERE [IdEmpresa] = '09d17ec1-956c-4c6e-85b3-317c7cce4e01'
             AND [SlugNormalizado] <> N'nexora-legado')
    THROW 51002, 'El Id de la empresa legada ya existe con otro slug.', 1;

IF NOT EXISTS (SELECT 1 FROM [Empresas]
               WHERE [IdEmpresa] = '09d17ec1-956c-4c6e-85b3-317c7cce4e01')
    INSERT INTO [Empresas]
        ([IdEmpresa], [SlugNormalizado], [NombreComercial], [Rnc],
         [Estado], [ZonaHoraria], [Moneda], [FechaCreacionUtc])
    VALUES
        ('09d17ec1-956c-4c6e-85b3-317c7cce4e01', N'nexora-legado',
         N'Espacio inicial de compatibilidad', NULL,
         N'PendienteConfiguracion', N'America/Santo_Domingo', N'DOP', SYSUTCDATETIME());
");

            // IdEmpresa NOT NULL + default temporal: datos previos y nuevos
            // del mononegocio quedan asignados a la empresa inicial.
            // Aún NO se permite crear una segunda empresa desde la aplicación.
            migrationBuilder.AddColumn<Guid>(
                name: "IdEmpresa",
                table: "Categorias",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: EmpresaLegada);

            migrationBuilder.AddColumn<Guid>(
                name: "IdEmpresa",
                table: "Clientes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: EmpresaLegada);

            migrationBuilder.AddColumn<Guid>(
                name: "IdEmpresa",
                table: "Productos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: EmpresaLegada);

            migrationBuilder.AddColumn<Guid>(
                name: "IdEmpresa",
                table: "Facturas",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: EmpresaLegada);

            migrationBuilder.AddColumn<Guid>(
                name: "IdEmpresa",
                table: "DetallesFactura",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: EmpresaLegada);

            migrationBuilder.AddColumn<Guid>(
                name: "IdEmpresa",
                table: "Pagos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: EmpresaLegada);

            migrationBuilder.AddColumn<Guid>(
                name: "IdEmpresa",
                table: "CasosPosventa",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: EmpresaLegada);

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_IdEmpresa",
                table: "Categorias",
                column: "IdEmpresa");

            migrationBuilder.AddForeignKey(
                name: "FK_Categorias_Empresas_IdEmpresa",
                table: "Categorias",
                column: "IdEmpresa",
                principalTable: "Empresas",
                principalColumn: "IdEmpresa",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_IdEmpresa",
                table: "Clientes",
                column: "IdEmpresa");

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_Empresas_IdEmpresa",
                table: "Clientes",
                column: "IdEmpresa",
                principalTable: "Empresas",
                principalColumn: "IdEmpresa",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_IdEmpresa",
                table: "Productos",
                column: "IdEmpresa");

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Empresas_IdEmpresa",
                table: "Productos",
                column: "IdEmpresa",
                principalTable: "Empresas",
                principalColumn: "IdEmpresa",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_IdEmpresa",
                table: "Facturas",
                column: "IdEmpresa");

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_Empresas_IdEmpresa",
                table: "Facturas",
                column: "IdEmpresa",
                principalTable: "Empresas",
                principalColumn: "IdEmpresa",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.CreateIndex(
                name: "IX_DetallesFactura_IdEmpresa",
                table: "DetallesFactura",
                column: "IdEmpresa");

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesFactura_Empresas_IdEmpresa",
                table: "DetallesFactura",
                column: "IdEmpresa",
                principalTable: "Empresas",
                principalColumn: "IdEmpresa",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_IdEmpresa",
                table: "Pagos",
                column: "IdEmpresa");

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_Empresas_IdEmpresa",
                table: "Pagos",
                column: "IdEmpresa",
                principalTable: "Empresas",
                principalColumn: "IdEmpresa",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.CreateIndex(
                name: "IX_CasosPosventa_IdEmpresa",
                table: "CasosPosventa",
                column: "IdEmpresa");

            migrationBuilder.AddForeignKey(
                name: "FK_CasosPosventa_Empresas_IdEmpresa",
                table: "CasosPosventa",
                column: "IdEmpresa",
                principalTable: "Empresas",
                principalColumn: "IdEmpresa",
                onDelete: ReferentialAction.NoAction);

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Respetar relaciones: quitar FKs antes que los campos.
            migrationBuilder.DropForeignKey(
                name: "FK_Categorias_Empresas_IdEmpresa",
                table: "Categorias");

            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_Empresas_IdEmpresa",
                table: "Clientes");

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Empresas_IdEmpresa",
                table: "Productos");

            migrationBuilder.DropForeignKey(
                name: "FK_Facturas_Empresas_IdEmpresa",
                table: "Facturas");

            migrationBuilder.DropForeignKey(
                name: "FK_DetallesFactura_Empresas_IdEmpresa",
                table: "DetallesFactura");

            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_Empresas_IdEmpresa",
                table: "Pagos");

            migrationBuilder.DropForeignKey(
                name: "FK_CasosPosventa_Empresas_IdEmpresa",
                table: "CasosPosventa");

            migrationBuilder.DropIndex(
                name: "IX_Categorias_IdEmpresa",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "IdEmpresa",
                table: "Categorias");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_IdEmpresa",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "IdEmpresa",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Productos_IdEmpresa",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "IdEmpresa",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Facturas_IdEmpresa",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "IdEmpresa",
                table: "Facturas");

            migrationBuilder.DropIndex(
                name: "IX_DetallesFactura_IdEmpresa",
                table: "DetallesFactura");

            migrationBuilder.DropColumn(
                name: "IdEmpresa",
                table: "DetallesFactura");

            migrationBuilder.DropIndex(
                name: "IX_Pagos_IdEmpresa",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "IdEmpresa",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "IX_CasosPosventa_IdEmpresa",
                table: "CasosPosventa");

            migrationBuilder.DropColumn(
                name: "IdEmpresa",
                table: "CasosPosventa");

            // No borrar una empresa vinculada a una membresía creada después.
            migrationBuilder.Sql(@"
DELETE FROM [Empresas]
WHERE [IdEmpresa] = '09d17ec1-956c-4c6e-85b3-317c7cce4e01'
  AND [SlugNormalizado] = N'nexora-legado'
  AND NOT EXISTS (
      SELECT 1 FROM [EmpresaUsuarios]
      WHERE [IdEmpresa] = '09d17ec1-956c-4c6e-85b3-317c7cce4e01');
");
        }
    }
}

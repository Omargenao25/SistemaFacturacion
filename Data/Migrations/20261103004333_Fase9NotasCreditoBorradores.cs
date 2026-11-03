using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFacturacion.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fase9NotasCreditoBorradores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotasCredito",
                columns: table => new
                {
                    IdNotaCredito = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdEmpresa = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdFactura = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Impuesto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Descuento = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaCreacionUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasCredito", x => x.IdNotaCredito);
                    table.UniqueConstraint("AK_NotasCredito_IdEmpresa_IdNotaCredito", x => new { x.IdEmpresa, x.IdNotaCredito });
                    table.CheckConstraint("CK_NotasCredito_Estado", "[Estado] = N'Borrador'");
                    table.CheckConstraint("CK_NotasCredito_Importes", "[Subtotal] >= 0 AND [Impuesto] >= 0 AND [Descuento] >= 0 AND [Total] >= 0");
                    table.ForeignKey(
                        name: "FK_NotasCredito_Empresas_IdEmpresa",
                        column: x => x.IdEmpresa,
                        principalTable: "Empresas",
                        principalColumn: "IdEmpresa");
                    table.ForeignKey(
                        name: "FK_NotasCredito_Facturas_IdEmpresa_IdFactura",
                        columns: x => new { x.IdEmpresa, x.IdFactura },
                        principalTable: "Facturas",
                        principalColumns: new[] { "IdEmpresa", "IdFactura" });
                });

            migrationBuilder.CreateTable(
                name: "DetallesNotaCredito",
                columns: table => new
                {
                    IdDetalleNotaCredito = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdEmpresa = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdNotaCredito = table.Column<int>(type: "int", nullable: false),
                    IdDetalleFactura = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Impuesto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Descuento = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesNotaCredito", x => x.IdDetalleNotaCredito);
                    table.CheckConstraint("CK_DetallesNotaCredito_Importes", "[Cantidad] > 0 AND [Subtotal] >= 0 AND [Impuesto] >= 0 AND [Descuento] >= 0 AND [Total] >= 0");
                    table.ForeignKey(
                        name: "FK_DetallesNotaCredito_DetallesFactura_IdEmpresa_IdDetalleFactura",
                        columns: x => new { x.IdEmpresa, x.IdDetalleFactura },
                        principalTable: "DetallesFactura",
                        principalColumns: new[] { "IdEmpresa", "IdDetalle" });
                    table.ForeignKey(
                        name: "FK_DetallesNotaCredito_Empresas_IdEmpresa",
                        column: x => x.IdEmpresa,
                        principalTable: "Empresas",
                        principalColumn: "IdEmpresa");
                    table.ForeignKey(
                        name: "FK_DetallesNotaCredito_NotasCredito_IdEmpresa_IdNotaCredito",
                        columns: x => new { x.IdEmpresa, x.IdNotaCredito },
                        principalTable: "NotasCredito",
                        principalColumns: new[] { "IdEmpresa", "IdNotaCredito" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesNotaCredito_IdEmpresa",
                table: "DetallesNotaCredito",
                column: "IdEmpresa");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesNotaCredito_IdEmpresa_IdDetalleFactura",
                table: "DetallesNotaCredito",
                columns: new[] { "IdEmpresa", "IdDetalleFactura" });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesNotaCredito_IdEmpresa_IdNotaCredito",
                table: "DetallesNotaCredito",
                columns: new[] { "IdEmpresa", "IdNotaCredito" });

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_IdEmpresa",
                table: "NotasCredito",
                column: "IdEmpresa");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_IdEmpresa_IdFactura",
                table: "NotasCredito",
                columns: new[] { "IdEmpresa", "IdFactura" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetallesNotaCredito");

            migrationBuilder.DropTable(
                name: "NotasCredito");
        }
    }
}

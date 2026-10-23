using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFacturacion.Data.Migrations
{
    /// <summary>
    /// Solicitudes de reembolso, sin salida real de dinero ni acción
    /// para marcar como pagadas (pendiente de la autorización de Russell).
    /// </summary>
    public partial class CrearReembolsosPendientes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Reembolsos",
                columns: table => new
                {
                    IdReembolso = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdEmpresa = table.Column<Guid>(
                        type: "uniqueidentifier", nullable: false,
                        defaultValue: new Guid("09d17ec1-956c-4c6e-85b3-317c7cce4e01")),
                    IdFactura = table.Column<int>(type: "int", nullable: false),
                    MontoSolicitado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MetodoPagoOriginal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    FechaSolicitudUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaConfirmacionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmadoPorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReferenciaDevolucion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reembolsos", x => x.IdReembolso);
                    table.CheckConstraint(
                        "CK_Reembolsos_Estado",
                        "[Estado] IN (N'Pendiente', N'PendienteRevision', N'Aprobado', N'Procesando', N'Reembolsado', N'Rechazado', N'Fallido')");
                    table.CheckConstraint("CK_Reembolsos_MontoSolicitado", "[MontoSolicitado] >= 0");
                    table.ForeignKey(
                        name: "FK_Reembolsos_Empresas_IdEmpresa",
                        column: x => x.IdEmpresa,
                        principalTable: "Empresas",
                        principalColumn: "IdEmpresa",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_Reembolsos_Facturas_IdEmpresa_IdFactura",
                        columns: x => new { x.IdEmpresa, x.IdFactura },
                        principalTable: "Facturas",
                        principalColumns: new[] { "IdEmpresa", "IdFactura" },
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reembolsos_IdEmpresa",
                table: "Reembolsos",
                column: "IdEmpresa");

            // Garantía de una sola solicitud por anulación, incluso con
            // peticiones simultáneas o reintentos.
            migrationBuilder.CreateIndex(
                name: "IX_Reembolsos_IdEmpresa_IdFactura",
                table: "Reembolsos",
                columns: new[] { "IdEmpresa", "IdFactura" },
                unique: true);

            // No se crean devoluciones para facturas anuladas ANTES de esta
            // migración: requieren reconciliación documentada y autorizada.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Reembolsos");
        }
    }
}

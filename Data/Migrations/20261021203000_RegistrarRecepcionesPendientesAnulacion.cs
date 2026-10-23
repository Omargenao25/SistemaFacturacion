using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFacturacion.Data.Migrations
{
    /// <summary>
    /// Añade obligaciones de recepción por línea. No altera stock histórico
    /// ni asigna recepciones ficticias a facturas previamente anuladas.
    /// </summary>
    public partial class RegistrarRecepcionesPendientesAnulacion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecepcionesAnulacion",
                columns: table => new
                {
                    IdRecepcion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdEmpresa = table.Column<Guid>(
                        type: "uniqueidentifier", nullable: false,
                        defaultValue: new Guid("09d17ec1-956c-4c6e-85b3-317c7cce4e01")),
                    IdDetalle = table.Column<int>(type: "int", nullable: false),
                    CantidadEsperada = table.Column<int>(type: "int", nullable: false),
                    CantidadRecibida = table.Column<int>(type: "int", nullable: false),
                    CantidadLiberada = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    FechaCreacionUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaRecepcionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecibidoPorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecepcionesAnulacion", x => x.IdRecepcion);
                    table.CheckConstraint("CK_RecepcionesAnulacion_Estado",
                        "[Estado] IN (N'Pendiente', N'Parcial', N'EnInspeccion', N'Completada', N'NoRecibida')");
                    table.CheckConstraint("CK_RecepcionesAnulacion_Cantidades",
                        "[CantidadEsperada] > 0 AND [CantidadRecibida] >= 0 " +
                        "AND [CantidadLiberada] >= 0 AND [CantidadLiberada] <= [CantidadRecibida] " +
                        "AND [CantidadRecibida] <= [CantidadEsperada]");
                    table.ForeignKey(
                        name: "FK_RecepcionesAnulacion_Empresas_IdEmpresa",
                        column: x => x.IdEmpresa,
                        principalTable: "Empresas",
                        principalColumn: "IdEmpresa",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_RecepcionesAnulacion_DetallesFactura_IdEmpresa_IdDetalle",
                        columns: x => new { x.IdEmpresa, x.IdDetalle },
                        principalTable: "DetallesFactura",
                        principalColumns: new[] { "IdEmpresa", "IdDetalle" },
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecepcionesAnulacion_IdEmpresa",
                table: "RecepcionesAnulacion",
                column: "IdEmpresa");

            migrationBuilder.CreateIndex(
                name: "IX_RecepcionesAnulacion_IdEmpresa_IdDetalle",
                table: "RecepcionesAnulacion",
                columns: new[] { "IdEmpresa", "IdDetalle" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RecepcionesAnulacion");
        }
    }
}

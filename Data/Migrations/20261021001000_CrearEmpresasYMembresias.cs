using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFacturacion.Data.Migrations
{
    // Fase SaaS 1A: solo nuevas tablas. Sin cambios en datos comerciales.
    public partial class CrearEmpresasYMembresias : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    IdEmpresa = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SlugNormalizado = table.Column<string>(type: "nvarchar(63)", maxLength: 63, nullable: false),
                    NombreComercial = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Rnc = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ZonaHoraria = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Moneda = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    FechaCreacionUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.IdEmpresa);
                    table.CheckConstraint("CK_Empresas_Estado",
                        "[Estado] IN (N'PendienteConfiguracion', N'Activa', N'Suspendida', N'Archivada')");
                });

            migrationBuilder.CreateTable(
                name: "EmpresaUsuarios",
                columns: table => new
                {
                    IdEmpresa = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaAltaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaBajaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmpresaUsuarios", x => new { x.IdEmpresa, x.UserId });
                    table.CheckConstraint("CK_EmpresaUsuarios_Estado",
                        "[Estado] IN (N'Invitado', N'Activo', N'Suspendido')");
                    table.CheckConstraint("CK_EmpresaUsuarios_Rol",
                        "[Rol] IN (N'Propietario', N'Administrador', N'Cajero')");
                    table.ForeignKey(
                        name: "FK_EmpresaUsuarios_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_EmpresaUsuarios_Empresas_IdEmpresa",
                        column: x => x.IdEmpresa,
                        principalTable: "Empresas",
                        principalColumn: "IdEmpresa",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_SlugNormalizado",
                table: "Empresas",
                column: "SlugNormalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmpresaUsuarios_UserId",
                table: "EmpresaUsuarios",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EmpresaUsuarios");
            migrationBuilder.DropTable(name: "Empresas");
        }
    }
}

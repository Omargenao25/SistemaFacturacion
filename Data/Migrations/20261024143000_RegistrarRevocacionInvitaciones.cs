using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SistemaFacturacion.Data;

#nullable disable

namespace SistemaFacturacion.Data.Migrations
{
    /// <summary>
    /// Amplía el catálogo de acciones auditables sin modificar entidades EF.
    /// El estado Revocada ya está permitido en InvitacionesEmpresa.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261024143000_RegistrarRevocacionInvitaciones")]
    public partial class RegistrarRevocacionInvitaciones : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE dbo.AuditoriaMembresias
    DROP CONSTRAINT CK_AuditoriaMembresias_Accion;

ALTER TABLE dbo.AuditoriaMembresias
    WITH CHECK ADD CONSTRAINT CK_AuditoriaMembresias_Accion CHECK (
        Accion IN (N'InvitacionCreada', N'InvitacionAceptada',
                   N'InvitacionRevocada', N'RolCambiado',
                   N'Suspendido', N'Reactivado'));");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bloquear rollback cuando haya evidencias de revocación.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM dbo.AuditoriaMembresias
           WHERE Accion = N'InvitacionRevocada')
    THROW 51001,
        'No puede revertirse la restricción sin conservar antes las auditorías de revocación.', 1;

ALTER TABLE dbo.AuditoriaMembresias
    DROP CONSTRAINT CK_AuditoriaMembresias_Accion;

ALTER TABLE dbo.AuditoriaMembresias
    WITH CHECK ADD CONSTRAINT CK_AuditoriaMembresias_Accion CHECK (
        Accion IN (N'InvitacionCreada', N'InvitacionAceptada',
                   N'RolCambiado', N'Suspendido', N'Reactivado'));");
        }
    }
}

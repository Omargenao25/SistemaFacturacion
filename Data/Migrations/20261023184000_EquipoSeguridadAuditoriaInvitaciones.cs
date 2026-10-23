using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SistemaFacturacion.Data;

#nullable disable

namespace SistemaFacturacion.Data.Migrations
{
    /// <summary>
    /// Tablas administrativas sin DbSet: gestionadas por SQL parametrizado
    /// para que auditoría e invitación formen parte de la misma transacción.
    /// No modifica el modelo EF ni reconstruye información heredada.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261023184000_EquipoSeguridadAuditoriaInvitaciones")]
    public partial class EquipoSeguridadAuditoriaInvitaciones : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE dbo.InvitacionesEmpresa (
    IdInvitacion uniqueidentifier NOT NULL CONSTRAINT PK_InvitacionesEmpresa PRIMARY KEY,
    IdEmpresa uniqueidentifier NOT NULL,
    EmailNormalizado nvarchar(256) NOT NULL,
    Rol nvarchar(30) NOT NULL,
    TokenHash varbinary(32) NOT NULL,
    Estado nvarchar(20) NOT NULL CONSTRAINT DF_InvitacionesEmpresa_Estado DEFAULT N'Pendiente',
    CreadoPorUserId nvarchar(450) NOT NULL,
    CreadoUtc datetime2(7) NOT NULL,
    VenceUtc datetime2(7) NOT NULL,
    ConsumidoUtc datetime2(7) NULL,
    ConsumidoPorUserId nvarchar(450) NULL,
    CONSTRAINT FK_InvitacionesEmpresa_Empresas FOREIGN KEY (IdEmpresa)
        REFERENCES dbo.Empresas(IdEmpresa),
    CONSTRAINT CK_InvitacionesEmpresa_Rol CHECK (Rol IN (N'Administrador', N'Cajero')),
    CONSTRAINT CK_InvitacionesEmpresa_Estado CHECK (
        Estado IN (N'Pendiente', N'Aceptada', N'Revocada', N'Caducada')),
    CONSTRAINT CK_InvitacionesEmpresa_Plazo CHECK (VenceUtc > CreadoUtc)
);
CREATE UNIQUE INDEX UX_InvitacionesEmpresa_TokenHash
    ON dbo.InvitacionesEmpresa(TokenHash);
CREATE UNIQUE INDEX UX_InvitacionesEmpresa_PendienteEmail
    ON dbo.InvitacionesEmpresa(IdEmpresa, EmailNormalizado)
    WHERE Estado = N'Pendiente';
CREATE INDEX IX_InvitacionesEmpresa_EmpresaEstado
    ON dbo.InvitacionesEmpresa(IdEmpresa, Estado, VenceUtc);

CREATE TABLE dbo.AuditoriaMembresias (
    IdAuditoria bigint IDENTITY(1,1) NOT NULL
        CONSTRAINT PK_AuditoriaMembresias PRIMARY KEY,
    IdEmpresa uniqueidentifier NOT NULL,
    ActorUserId nvarchar(450) NOT NULL,
    AfectadoUserId nvarchar(450) NULL,
    IdInvitacion uniqueidentifier NULL,
    Accion nvarchar(40) NOT NULL,
    RolAnterior nvarchar(30) NULL,
    RolNuevo nvarchar(30) NULL,
    EstadoAnterior nvarchar(20) NULL,
    EstadoNuevo nvarchar(20) NULL,
    FechaUtc datetime2(7) NOT NULL
        CONSTRAINT DF_AuditoriaMembresias_FechaUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_AuditoriaMembresias_Empresas FOREIGN KEY (IdEmpresa)
        REFERENCES dbo.Empresas(IdEmpresa),
    CONSTRAINT CK_AuditoriaMembresias_Accion CHECK (
        Accion IN (N'InvitacionCreada', N'InvitacionAceptada',
                   N'RolCambiado', N'Suspendido', N'Reactivado'))
);
CREATE INDEX IX_AuditoriaMembresias_EmpresaFecha
    ON dbo.AuditoriaMembresias(IdEmpresa, FechaUtc DESC, IdAuditoria DESC);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS dbo.AuditoriaMembresias;
DROP TABLE IF EXISTS dbo.InvitacionesEmpresa;");
        }
    }
}

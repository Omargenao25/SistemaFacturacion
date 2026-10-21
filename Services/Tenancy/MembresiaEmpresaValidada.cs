using System;

namespace SistemaFacturacion.Services.Tenancy
{
    /// <summary>
    /// Resultado de una comprobación de acceso en SQL Server.
    /// No representa una autorización de operaciones específicas del módulo.
    /// </summary>
    public sealed record MembresiaEmpresaValidada(Guid IdEmpresa, string Rol);
}

using System;

namespace SistemaFacturacion.Data
{
    /// <summary>
    /// Empresa de compatibilidad para datos mononegocio anteriores a Nexora SaaS.
    /// No representa un usuario autorizado, ni una empresa lista para operar.
    /// ATENCIÓN: eliminar los defaults de esta empresa al activar tenants reales.
    /// </summary>
    internal static class CompatibilidadEmpresaInicial
    {
        internal static readonly Guid Id =
            new("09d17ec1-956c-4c6e-85b3-317c7cce4e01");
    }
}

using System;

namespace SistemaFacturacion.Services.Tenancy
{
    /// <summary>
    /// Identificador de la empresa autorizado por el servidor para esta operación.
    /// Guid.Empty indica SIN autorización empresarial y debe fallar cerrado.
    /// </summary>
    public interface IContextoEmpresa
    {
        Guid IdEmpresa { get; }
        bool TieneEmpresa { get; }
    }
}

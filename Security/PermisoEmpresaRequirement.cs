using Microsoft.AspNetCore.Authorization;

namespace SistemaFacturacion.Security
{
    public sealed class PermisoEmpresaRequirement : IAuthorizationRequirement
    {
        public string NombrePermiso { get; }

        public PermisoEmpresaRequirement(string nombrePermiso)
        {
            NombrePermiso = nombrePermiso;
        }
    }
}

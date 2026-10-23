using Microsoft.AspNetCore.Authorization;
using SistemaFacturacion.Services.Tenancy;

namespace SistemaFacturacion.Security
{
    /// <summary>
    /// Autoriza solo con una membresía verificada en ESTE request y con
    /// identidad autenticada. No utiliza User.IsInRole ni claims comerciales
    /// persistentes susceptibles a suspensiones o cambios de empresa.
    /// </summary>
    public sealed class PermisoEmpresaHandler :
        AuthorizationHandler<PermisoEmpresaRequirement>
    {
        private readonly ContextoEmpresaVerificado _contexto;

        public PermisoEmpresaHandler(ContextoEmpresaVerificado contexto)
        {
            _contexto = contexto;
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermisoEmpresaRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated == true &&
                _contexto.TieneEmpresa &&
                PermisosNexora.TienePermiso(
                    _contexto.RolEmpresa, requirement.NombrePermiso))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}

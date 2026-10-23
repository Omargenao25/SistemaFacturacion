using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SistemaFacturacion.Security
{
    /// <summary>
    /// Solo se registra cuando la opción de tenancy validada está activada.
    /// Protege CADA acción MVC conocida usando su policy empresarial.
    /// Por defecto, acciones o verbos desconocidos se deniegan.
    /// Este filtro NO habilita operaciones nuevas sobre dinero/stock.
    /// </summary>
    public sealed class FiltroPermisosComerciales : IAsyncAuthorizationFilter
    {
        private readonly IAuthorizationService _autorizacion;

        public FiltroPermisosComerciales(IAuthorizationService autorizacion)
        {
            _autorizacion = autorizacion;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
            var permiso = ReglasAccesoControladores.ObtenerPermiso(
                descriptor?.ControllerName,
                descriptor?.ActionName,
                context.HttpContext.Request.Method);

            // No activar políticas con un controlador desconocido, aunque
            // sus endpoints no consulten datos comerciales.
            if (permiso == null)
            {
                context.Result = context.HttpContext.User.Identity?.IsAuthenticated == true
                    ? new ForbidResult()
                    : new ChallengeResult();
                return;
            }

            // Usa el mismo handler y contexto scoped creado por el
            // verificador SQL; no utiliza roles globales de ASP.NET Identity.
            var resultado = await _autorizacion.AuthorizeAsync(
                context.HttpContext.User, resource: null, policyName: permiso);

            if (!resultado.Succeeded)
            {
                context.Result = context.HttpContext.User.Identity?.IsAuthenticated == true
                    ? new ForbidResult()
                    : new ChallengeResult();
            }
        }
    }
}

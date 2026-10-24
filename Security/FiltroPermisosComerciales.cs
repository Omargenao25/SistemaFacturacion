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
            // La persona invitada todavía NO pertenece a la empresa.
            // ÚNICA excepción al filtro tenant: aceptar invitación por Identity,
            // GET/POST exactos. Las acciones llevan [Authorize] en su controller;
            // los POST tienen antiforgery y el servicio comprueba token + email.
            if (descriptor?.ControllerName == "Invitaciones" &&
                descriptor.ActionName == "Aceptar" &&
                context.HttpContext.Request.Method is "GET" or "POST")
            {
                if (context.HttpContext.User.Identity?.IsAuthenticated != true)
                    context.Result = new ChallengeResult();
                return;
            }

            // La selección de empresa aún no tiene tenant. Permitir
            // exclusivamente el GET de lectura para un usuario autenticado;
            // el propio controlador consulta SOLO las membresías activas de
            // ese usuario. NO autorizar POST ni ningún método arbitrario.
            if (descriptor?.ControllerName == "EmpresasAcceso" &&
                descriptor.ActionName == "Index" &&
                context.HttpContext.Request.Method == "GET")
            {
                if (context.HttpContext.User.Identity?.IsAuthenticated != true)
                    context.Result = new ChallengeResult();
                return;
            }

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

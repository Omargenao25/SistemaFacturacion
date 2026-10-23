using Microsoft.AspNetCore.Http;

namespace SistemaFacturacion.Services.Tenancy
{
    /// <summary>
    /// Obtiene únicamente el slug de la RUTA seleccionada. No acepta IdEmpresa,
    /// roles ni encabezados enviados por el navegador como autorización.
    /// Solo SQL puede aprobar empresa y rol en el contexto scoped.
    /// Se ejecuta después de UseRouting y UseAuthentication.
    /// </summary>
    public sealed class VerificacionMembresiaMiddleware
    {
        private readonly RequestDelegate _next;

        public VerificacionMembresiaMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext http,
            ContextoEmpresaVerificado contexto,
            IValidadorAccesoEmpresa verificador)
        {
            // Sin ruta de empresa, el contexto permanece vacío. No se concede
            // un tenant por defecto y el policy handler deniega su utilización.
            var slugCandidato = http.Request.RouteValues.TryGetValue(
                "empresaSlug", out var valor)
                ? valor as string : null;

            if (slugCandidato != null &&
                http.User.Identity?.IsAuthenticated == true)
            {
                var membresia = await verificador.ValidarAsync(
                    http.User, slugCandidato, http.RequestAborted);

                if (membresia != null)
                    contexto.Establecer(membresia);
            }

            await _next(http);
        }
    }
}

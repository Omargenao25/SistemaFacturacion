using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using SistemaFacturacion.Services.Tenancy;

namespace SistemaFacturacion.Security
{
    /// <summary>
    /// En modo SaaS, un POST comercial debe redirigir SOLO al tenant que
    /// el middleware validó para esta solicitud. Evita que RedirectToAction
    /// genere /Clientes, /Facturas, etc. sin /e/{empresaSlug}/.
    /// No selecciona ni valida empresas: exige contexto ya validado.
    /// </summary>
    public sealed class PreservarRutaEmpresaFiltro : IAsyncResultFilter
    {
        private readonly ContextoEmpresaVerificado _empresa;

        public PreservarRutaEmpresaFiltro(ContextoEmpresaVerificado empresa)
        {
            _empresa = empresa;
        }

        public async Task OnResultExecutionAsync(
            ResultExecutingContext context, ResultExecutionDelegate next)
        {
            if (_empresa.TieneEmpresa &&
                context.Result is RedirectToActionResult redirect &&
                !EsDestinoPublico(redirect.ControllerName))
            {
                var slug = context.RouteData.Values["empresaSlug"]?.ToString();
                if (SlugEmpresaSeguro.EsValido(slug))
                {
                    // Sobrescribe incluso un slug forjado en la acción. La
                    // identidad del tenant procede de la petición validada.
                    redirect.RouteValues = ConservarEmpresa(
                        redirect.RouteValues, slug!);
                }
                else
                {
                    // No seguir redirecciones comerciales con contexto roto.
                    context.Result = new ForbidResult();
                }
            }

            await next();
        }

        public static RouteValueDictionary ConservarEmpresa(
            object? valores, string slugValidado)
        {
            if (!SlugEmpresaSeguro.EsValido(slugValidado))
                throw new ArgumentException("Slug empresarial inválido.", nameof(slugValidado));

            var ruta = new RouteValueDictionary(valores);
            ruta["empresaSlug"] = slugValidado;
            return ruta;
        }

        private static bool EsDestinoPublico(string? controlador) =>
            controlador is "EmpresasAcceso" or "Invitaciones";
    }
}

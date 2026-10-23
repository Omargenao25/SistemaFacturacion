using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFacturacion.Security;
using SistemaFacturacion.Services.Tenancy;

namespace SistemaFacturacion.Controllers
{
    /// <summary>
    /// Prueba controlada del pipeline seguro; NO concede acceso a acciones
    /// comerciales existentes ni cambia su estado. Solo responde a la ruta
    /// con slug, cookie Identity y membresía activa verificada en SQL Server.
    /// </summary>
    public sealed class SeguridadEmpresaController : Controller
    {
        private readonly ContextoEmpresaVerificado _empresa;

        public SeguridadEmpresaController(ContextoEmpresaVerificado empresa)
        {
            _empresa = empresa;
        }

        [HttpGet("e/{empresaSlug}/seguridad/estado")]
        [Authorize(Policy = PermisosNexora.ConsultarOperaciones)]
        public IActionResult Estado()
        {
            // No se exponen registros de otros tenants, secretos ni UserId.
            return Json(new
            {
                empresaValidada = _empresa.TieneEmpresa,
                idEmpresa = _empresa.IdEmpresa,
                rolEmpresa = _empresa.RolEmpresa,
                permisosActivos = "DiagnosticoSoloLectura"
            });
        }
    }
}

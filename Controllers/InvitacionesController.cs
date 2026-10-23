using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SistemaFacturacion.Services.Tenancy;

namespace SistemaFacturacion.Controllers
{
    /// <summary>
    /// Aceptación con token de un solo uso y correo Identity confirmado.
    /// La persona invitada todavía NO tiene membresía; estas dos rutas
    /// requieren Identity pero están exentas solo del filtro empresarial MVC.
    /// </summary>
    [Authorize]
    [Route("invitaciones")]
    public sealed class InvitacionesController : Controller
    {
        private readonly UserManager<IdentityUser> _usuarios;
        private readonly GestionMembresiasSegura _gestion;
        private readonly IConfiguration _config;
        private readonly ILogger<InvitacionesController> _log;

        public InvitacionesController(
            UserManager<IdentityUser> usuarios,
            GestionMembresiasSegura gestion,
            IConfiguration config,
            ILogger<InvitacionesController> log)
        {
            _usuarios = usuarios;
            _gestion = gestion;
            _config = config;
            _log = log;
        }

        [HttpGet("aceptar")]
        public IActionResult Aceptar(string? token)
        {
            RestringirCache();
            if (!_config.GetValue<bool>("Seguridad:ActivarTenancyValidada"))
                return NotFound();
            if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
                return BadRequest("Enlace no válido.");
            return View(model: token);
        }

        [HttpPost("aceptar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aceptar(
            string token, CancellationToken cancellationToken)
        {
            RestringirCache();
            if (!_config.GetValue<bool>("Seguridad:ActivarTenancyValidada"))
                return NotFound();
            var usuario = await _usuarios.GetUserAsync(User);
            if (usuario == null)
                return Challenge();
            try
            {
                await _gestion.AceptarAsync(
                    usuario.Id, usuario.NormalizedEmail ?? "",
                    usuario.EmailConfirmed, token ?? "", cancellationToken);
                return View("Confirmada");
            }
            catch (InvalidOperationException ex)
            {
                // Errores de validación sin divulgar el destino del token.
                ModelState.AddModelError("", ex.Message);
                return View(model: token);
            }
            catch (SqlException ex)
            {
                _log.LogError(ex, "Error de aceptación de invitación");
                ModelState.AddModelError("",
                    "No fue posible completar esta invitación. Inténtalo más tarde.");
                return View(model: token);
            }
        }

        private void RestringirCache()
        {
            Response.Headers["Cache-Control"] = "no-store";
            Response.Headers["Referrer-Policy"] = "no-referrer";
            Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        }
    }
}

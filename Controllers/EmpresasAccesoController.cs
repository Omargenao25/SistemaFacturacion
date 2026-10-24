using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Security;
using SistemaFacturacion.ViewModels;

namespace SistemaFacturacion.Controllers
{
    /// <summary>
    /// Puerta de entrada autenticada al modo SaaS. NO establece el tenant:
    /// solo muestra las membresías actuales del usuario. Cada clic vuelve a
    /// pasar por el middleware SQL de pertenencia empresarial.
    /// </summary>
    [Authorize]
    [Route("mis-empresas")]
    public sealed class EmpresasAccesoController : Controller
    {
        private const int TamanoPagina = 12;
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;

        public EmpresasAccesoController(
            ApplicationDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        [HttpGet("")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Index(
            int pagina = 1, CancellationToken cancellationToken = default)
        {
            // No exponer el selector en el modo heredado mononegocio.
            if (!_config.GetValue<bool>("Seguridad:ActivarTenancyValidada"))
                return NotFound();

            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(usuarioId) || usuarioId.Length > 450)
                return Forbid();

            // EmpresaUsuario es un catálogo GLOBAL SIN filtro EF por tenant.
            // Filtrar siempre por usuario, membresía activa y empresa activa.
            var consulta = _db.EmpresaUsuarios.AsNoTracking()
                .Where(m => m.UserId == usuarioId &&
                            m.Estado == "Activo" &&
                            m.FechaBajaUtc == null &&
                            (m.Rol == "Propietario" ||
                             m.Rol == "Administrador" || m.Rol == "Cajero") &&
                            m.Empresa != null &&
                            m.Empresa.Estado == "Activa");

            var total = await consulta.CountAsync(cancellationToken);
            var totalPaginas = Math.Max(1, (total - 1) / TamanoPagina + 1);
            pagina = Math.Clamp(pagina, 1, totalPaginas);

            var empresas = await consulta
                .OrderBy(m => m.Empresa!.NombreComercial)
                .ThenBy(m => m.IdEmpresa)
                .Skip((pagina - 1) * TamanoPagina)
                .Take(TamanoPagina)
                .Select(m => new EmpresaAccesoItem
                {
                    Nombre = m.Empresa!.NombreComercial,
                    Slug = m.Empresa!.SlugNormalizado,
                    Rol = m.Rol,
                    Moneda = m.Empresa!.Moneda
                })
                .ToListAsync(cancellationToken);

            // Nunca enlazar a slugs malformados aunque SQL contenga
            // una fila corrupta. El validador SQL vuelve a comprobarlo.
            empresas = empresas.Where(e => SlugEmpresaSeguro.EsValido(e.Slug))
                .ToList();

            Response.Headers["Cache-Control"] = "private, no-store";
            Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            return View(new EmpresasAccesoViewModel
            {
                Pagina = pagina,
                TotalPaginas = totalPaginas,
                TotalEmpresas = total,
                Empresas = empresas
            });
        }
    }
}

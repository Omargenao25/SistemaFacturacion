using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Security;
using SistemaFacturacion.Services.Tenancy;
using SistemaFacturacion.ViewModels;

namespace SistemaFacturacion.Controllers
{
    /// <summary>
    /// Directorio y administración auditada de membresías para propietarios.
    /// EmpresaUsuarios es una tabla GLOBAL: siempre limitar su consulta al
    /// IdEmpresa verificado por el servidor, antes de contar y proyectar.
    /// </summary>
    [Authorize(Policy = PermisosNexora.AdministrarUsuarios)]
    public sealed class EquipoController : Controller
    {
        private const int TamanoPagina = 20;
        private readonly ApplicationDbContext _context;
        private readonly ContextoEmpresaVerificado _empresa;
        private readonly GestionMembresiasSegura _gestion;
        private readonly ILogger<EquipoController> _log;

        public EquipoController(
            ApplicationDbContext context,
            ContextoEmpresaVerificado empresa,
            GestionMembresiasSegura gestion,
            ILogger<EquipoController> log)
        {
            _context = context;
            _empresa = empresa;
            _gestion = gestion;
            _log = log;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? rol, string? estado, int pagina = 1,
            CancellationToken cancellationToken = default)
        {
            // Segunda comprobación deliberada: el filtro MVC y la política
            // protegen el endpoint, pero JAMÁS consultar una tabla global
            // cuando no existe una empresa confiable y un propietario.
            if (!_empresa.TieneEmpresa ||
                !PermisosNexora.TienePermiso(
                    _empresa.RolEmpresa, PermisosNexora.AdministrarUsuarios))
                return Forbid();

            var nombreEmpresa = await _context.Empresas
                .AsNoTracking()
                .Where(e => e.IdEmpresa == _empresa.IdEmpresa &&
                            e.Estado == "Activa")
                .Select(e => e.NombreComercial)
                .SingleOrDefaultAsync(cancellationToken);
            if (nombreEmpresa == null)
                return Forbid();

            rol = rol is "Propietario" or "Administrador" or "Cajero"
                ? rol : "";
            estado = estado is "Activo" or "Invitado" or "Suspendido" or "Baja"
                ? estado : "";

            // SIN IgnoreQueryFilters ni listados globales: EmpresaUsuarios
            // no tiene filtro EF de tenant por ser tabla de plataforma.
            var deEstaEmpresa = _context.EmpresaUsuarios.AsNoTracking()
                .Where(m => m.IdEmpresa == _empresa.IdEmpresa);

            var modelo = new EquipoEmpresaViewModel
            {
                Empresa = nombreEmpresa,
                FiltroRol = rol,
                FiltroEstado = estado,
                Activos = await deEstaEmpresa.CountAsync(
                    m => m.Estado == "Activo" && m.FechaBajaUtc == null,
                    cancellationToken),
                PropietariosActivos = await deEstaEmpresa.CountAsync(
                    m => m.Estado == "Activo" && m.FechaBajaUtc == null &&
                         m.Rol == "Propietario", cancellationToken),
                Invitados = await deEstaEmpresa.CountAsync(
                    m => m.Estado == "Invitado" && m.FechaBajaUtc == null,
                    cancellationToken),
                Suspendidos = await deEstaEmpresa.CountAsync(
                    m => m.Estado == "Suspendido" && m.FechaBajaUtc == null,
                    cancellationToken)
            };

            var consulta = deEstaEmpresa;
            if (!string.IsNullOrEmpty(rol))
                consulta = consulta.Where(m => m.Rol == rol);

            consulta = estado switch
            {
                "Activo" => consulta.Where(m =>
                    m.Estado == "Activo" && m.FechaBajaUtc == null),
                "Invitado" => consulta.Where(m =>
                    m.Estado == "Invitado" && m.FechaBajaUtc == null),
                "Suspendido" => consulta.Where(m =>
                    m.Estado == "Suspendido" && m.FechaBajaUtc == null),
                "Baja" => consulta.Where(m => m.FechaBajaUtc != null),
                _ => consulta
            };

            modelo.TotalMiembros = await consulta.CountAsync(cancellationToken);
            modelo.TotalPaginas = Math.Max(1,
                (modelo.TotalMiembros + TamanoPagina - 1) / TamanoPagina);
            modelo.Pagina = Math.Clamp(pagina, 1, modelo.TotalPaginas);

            // Incluye únicamente los datos indispensables para identificar
            // empleados. Nunca se devuelve UserId, hash, tokens ni stamps.
            modelo.Miembros = await consulta
                .OrderBy(m => m.Rol)
                .ThenBy(m => m.UserId)
                .Skip((modelo.Pagina - 1) * TamanoPagina)
                .Take(TamanoPagina)
                .Select(m => new MiembroEmpresaItem
                {
                    UserId = m.UserId,
                    RowVersion = m.RowVersion,
                    NombreUsuario = m.Usuario == null
                        ? "Usuario no disponible"
                        : (m.Usuario.UserName ?? "Usuario no disponible"),
                    Correo = m.Usuario == null
                        ? ""
                        : (m.Usuario.Email ?? ""),
                    CorreoConfirmado = m.Usuario != null &&
                                      m.Usuario.EmailConfirmed,
                    Rol = m.Rol,
                    Estado = m.Estado,
                    FechaAltaUtc = m.FechaAltaUtc,
                    FechaBajaUtc = m.FechaBajaUtc
                })
                .ToListAsync(cancellationToken);

            foreach (var permiso in PermisosNexora.Todos)
            {
                modelo.Matriz.Add(new PermisoRolItem
                {
                    Nombre = NombreLegible(permiso),
                    Permiso = permiso,
                    Propietario = PermisosNexora.TienePermiso(
                        "Propietario", permiso),
                    Administrador = PermisosNexora.TienePermiso(
                        "Administrador", permiso),
                    Cajero = PermisosNexora.TienePermiso("Cajero", permiso)
                });
            }

            var actorActual = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            modelo.AuditoriaReciente = await _gestion.UltimosEventosAsync(
                actorActual, cancellationToken);
            return View(modelo);
        }

        [HttpGet]
        public async Task<IActionResult> Invitaciones(
            string? estado = "Pendiente", int pagina = 1,
            CancellationToken cancellationToken = default)
        {
            // No leer tabla global sin tenant y autorización activa.
            if (!_empresa.TieneEmpresa ||
                !PermisosNexora.TienePermiso(
                    _empresa.RolEmpresa, PermisosNexora.AdministrarUsuarios))
                return Forbid();

            // Convertir el filtro opcional a un valor no nulo antes de
            // pasarlo al servicio, sin perder la lista cerrada de estados.
            string estadoValidado = estado is not null &&
                GestionMembresiasSegura.EstadoInvitacionPermitido(estado)
                ? estado : "Pendiente";

            try
            {
                var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
                var registros = await _gestion.ConsultarInvitacionesAsync(
                    actor, estadoValidado, pagina, cancellationToken);

                var nombre = await _context.Empresas.AsNoTracking()
                    .Where(e => e.IdEmpresa == _empresa.IdEmpresa &&
                                e.Estado == "Activa")
                    .Select(e => e.NombreComercial)
                    .SingleOrDefaultAsync(cancellationToken);

                if (nombre == null)
                    return Forbid();

                Response.Headers["Cache-Control"] = "private, no-store";
                return View(new InvitacionesEmpresaViewModel
                {
                    Empresa = nombre,
                    EstadoSeleccionado = estadoValidado,
                    Pagina = registros.Pagina,
                    TotalPaginas = Math.Max(1, (registros.Total + 19) / 20),
                    TotalResultados = registros.Total,
                    Invitaciones = registros.Registros
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevocarInvitacion(
            Guid idInvitacion, CancellationToken cancellationToken)
        {
            if (!_empresa.TieneEmpresa)
                return Forbid();

            try
            {
                var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
                await _gestion.RevocarInvitacionAsync(
                    actor, idInvitacion, cancellationToken);
                TempData["EquipoMensaje"] =
                    "Invitación revocada. Ese enlace ya no puede utilizarse.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["EquipoError"] = ex.Message;
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (SqlException ex)
            {
                _log.LogError(ex, "No fue posible revocar una invitación");
                TempData["EquipoError"] =
                    "No fue posible revocar la invitación. Consulta la bitácora.";
            }

            return RedirectToAction(nameof(Invitaciones), new
            {
                empresaSlug = RouteData.Values["empresaSlug"]
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = PermisosNexora.AdministrarRoles)]
        public async Task<IActionResult> CambiarRol(
            string usuarioId, string version, string nuevoRol,
            CancellationToken cancellationToken)
        {
            if (!_empresa.TieneEmpresa)
                return Forbid();
            try
            {
                var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
                var bytes = Convert.FromBase64String(version ?? "");
                await _gestion.CambiarAsync(actor, usuarioId, bytes,
                    "Rol", nuevoRol, cancellationToken);
                TempData["EquipoMensaje"] = "Rol actualizado y registrado en la auditoría.";
            }
            catch (FormatException)
            {
                TempData["EquipoError"] = "Actualiza la página antes de modificar un empleado.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["EquipoError"] = ex.Message;
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (SqlException ex)
            {
                _log.LogError(ex, "Fallo al cambiar rol de empleado");
                TempData["EquipoError"] = "No se pudo guardar el cambio. Consulta los registros.";
            }
            return RedirectToAction(nameof(Index), new
            {
                empresaSlug = RouteData.Values["empresaSlug"]
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(
            string usuarioId, string version, string accion,
            CancellationToken cancellationToken)
        {
            if (!_empresa.TieneEmpresa)
                return Forbid();
            try
            {
                var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
                var bytes = Convert.FromBase64String(version ?? "");
                await _gestion.CambiarAsync(actor, usuarioId, bytes,
                    accion, null, cancellationToken);
                TempData["EquipoMensaje"] = "Membresía actualizada y auditada.";
            }
            catch (FormatException)
            {
                TempData["EquipoError"] = "Actualiza la página antes de modificar un empleado.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["EquipoError"] = ex.Message;
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (SqlException ex)
            {
                _log.LogError(ex, "Fallo al modificar membresía");
                TempData["EquipoError"] = "No se pudo guardar el cambio. Consulta los registros.";
            }
            return RedirectToAction(nameof(Index), new
            {
                empresaSlug = RouteData.Values["empresaSlug"]
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearInvitacion(
            string correo, string rol, CancellationToken cancellationToken)
        {
            if (!_empresa.TieneEmpresa)
                return Forbid();
            try
            {
                var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
                var token = await _gestion.CrearInvitacionAsync(
                    actor, correo, rol, cancellationToken);
                // Entrega manual del enlace. Nunca persistir el token ni enviarlo
                // a TempData/cookies. No existe proveedor de correo configurado.
                Response.Headers["Cache-Control"] = "no-store";
                Response.Headers["Referrer-Policy"] = "no-referrer";
                // Construir URL usando únicamente una base pública configurada,
                // nunca el Host enviado en la petición HTTP.
                var ruta = Url.Action("Aceptar", "Invitaciones", new { token });
                if (string.IsNullOrEmpty(ruta))
                    throw new InvalidOperationException(
                        "Invitación registrada, pero no se pudo construir su ruta.");
                var baseConfigurada = HttpContext.RequestServices
                    .GetRequiredService<IConfiguration>()["Seguridad:BaseUrlPublica"];
                var enlace = ruta;
                if (!string.IsNullOrWhiteSpace(baseConfigurada))
                {
                    if (!Uri.TryCreate(baseConfigurada, UriKind.Absolute, out var urlBase) ||
                        (urlBase.Scheme != Uri.UriSchemeHttps &&
                         !(HttpContext.RequestServices
                             .GetRequiredService<IHostEnvironment>().IsDevelopment() &&
                           urlBase.Scheme == Uri.UriSchemeHttp &&
                           urlBase.IsLoopback)))
                        throw new InvalidOperationException(
                            "Configura Seguridad:BaseUrlPublica con una URL HTTPS válida.");
                    enlace = new Uri(urlBase, ruta).AbsoluteUri;
                }
                return View("EnlaceInvitacion", model: enlace);
            }
            catch (InvalidOperationException ex)
            {
                TempData["EquipoError"] = ex.Message;
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (SqlException ex)
            {
                _log.LogError(ex, "Fallo al registrar invitación");
                TempData["EquipoError"] = "No se pudo registrar la invitación.";
            }
            return RedirectToAction(nameof(Index), new
            {
                empresaSlug = RouteData.Values["empresaSlug"]
            });
        }

        private static string NombreLegible(string permiso) => permiso switch
        {
            PermisosNexora.ConsultarOperaciones => "Consultar operaciones",
            PermisosNexora.CrearFacturas => "Crear facturas",
            PermisosNexora.AnularFacturas => "Anular facturas",
            PermisosNexora.AdministrarInventario => "Administrar inventario",
            PermisosNexora.AdministrarClientes => "Administrar clientes",
            PermisosNexora.ConsultarRecepciones => "Consultar recepciones",
            PermisosNexora.ConsultarPosventa => "Consultar posventa",
            PermisosNexora.RegistrarPosventa => "Registrar casos posventa",
            PermisosNexora.ResolverPosventa => "Resolver casos posventa",
            PermisosNexora.ConsultarReembolsos => "Consultar reembolsos",
            PermisosNexora.ConsultarReportes => "Consultar reportes",
            PermisosNexora.AdministrarUsuarios => "Administrar usuarios",
            PermisosNexora.AdministrarRoles => "Administrar roles",
            PermisosNexora.AdministrarEmpresa => "Administrar empresa",
            PermisosNexora.ConfirmarRecepciones =>
                "Confirmar recepción (reservado)",
            PermisosNexora.LiberarInventario =>
                "Liberar inventario (reservado)",
            PermisosNexora.ConfirmarReembolsos =>
                "Confirmar reembolso (reservado)",
            _ => "Permiso no reconocido"
        };
    }
}

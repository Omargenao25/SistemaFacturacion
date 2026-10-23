using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Security;
using SistemaFacturacion.Services.Tenancy;
using SistemaFacturacion.ViewModels;

namespace SistemaFacturacion.Controllers
{
    /// <summary>
    /// Directorio empresarial de solo lectura para propietarios autenticados.
    /// EmpresaUsuarios es una tabla GLOBAL: siempre limitar su consulta al
    /// IdEmpresa verificado por el servidor, antes de contar y proyectar.
    /// </summary>
    [Authorize(Policy = PermisosNexora.AdministrarUsuarios)]
    public sealed class EquipoController : Controller
    {
        private const int TamanoPagina = 20;
        private readonly ApplicationDbContext _context;
        private readonly ContextoEmpresaVerificado _empresa;

        public EquipoController(
            ApplicationDbContext context,
            ContextoEmpresaVerificado empresa)
        {
            _context = context;
            _empresa = empresa;
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

            return View(modelo);
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

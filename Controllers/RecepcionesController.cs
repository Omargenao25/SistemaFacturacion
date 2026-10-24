using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Services.Inventario;
using SistemaFacturacion.ViewModels;

namespace SistemaFacturacion.Controllers
{
    /// <summary>
    /// Bandeja y simulación de inspección EXCLUSIVAMENTE de lectura.
    /// No hay POST, guardado, liberación de stock ni confirmación de recepción.
    /// La seguridad de escrituras dependerá de los roles por empresa.
    /// </summary>
    [Authorize]
    public sealed class RecepcionesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RecepcionesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? estado)
        {
            var consulta = _context.RecepcionesAnulacion
                .AsNoTracking()
                .Include(r => r.DetalleFactura)
                    .ThenInclude(d => d!.Producto)
                .Include(r => r.DetalleFactura)
                    .ThenInclude(d => d!.Factura)
                .AsQueryable();

            var estadosPermitidos = new[]
            {
                "Pendiente", "Parcial", "EnInspeccion", "Completada", "NoRecibida"
            };
            if (!string.IsNullOrWhiteSpace(estado) &&
                estadosPermitidos.Contains(estado))
                consulta = consulta.Where(r => r.Estado == estado);
            else
                estado = null;

            ViewBag.Estado = estado ?? "";
            return View(await consulta
                .OrderByDescending(r => r.FechaCreacionUtc)
                .ThenByDescending(r => r.IdRecepcion)
                .ToListAsync());
        }

        [HttpGet]
        public async Task<IActionResult> Details(
            int id,
            bool simular = false,
            int cantidadNueva = 0,
            int cantidadApta = 0,
            int cantidadCuarentena = 0,
            int cantidadReparacion = 0,
            int cantidadDescartada = 0)
        {
            // EF filtra empresa antes de resolver la solicitud.
            var recepcion = await _context.RecepcionesAnulacion
                .AsNoTracking()
                .Include(r => r.DetalleFactura)
                    .ThenInclude(d => d!.Factura)
                        .ThenInclude(f => f!.Cliente)
                .Include(r => r.DetalleFactura)
                    .ThenInclude(d => d!.Producto)
                .FirstOrDefaultAsync(r => r.IdRecepcion == id);

            if (recepcion == null)
                return NotFound();

            var modelo = new InspeccionRecepcionViewModel
            {
                Recepcion = recepcion,
                CantidadNueva = cantidadNueva,
                CantidadApta = cantidadApta,
                CantidadCuarentena = cantidadCuarentena,
                CantidadReparacion = cantidadReparacion,
                CantidadDescartada = cantidadDescartada
            };

            if (simular)
            {
                modelo.Simulacion = ReglasInspeccionMercancia.Evaluar(
                    recepcion.CantidadEsperada,
                    recepcion.CantidadRecibida,
                    cantidadNueva,
                    cantidadApta,
                    cantidadCuarentena,
                    cantidadReparacion,
                    cantidadDescartada);
            }

            // Renderizar HTML sin escribir datos ni inventario, incluso si
            // se envían parámetros GET arbitrarios desde el navegador.
            return View(modelo);
        }
    }
}

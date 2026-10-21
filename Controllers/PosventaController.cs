using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models;
using SistemaFacturacion.ViewModels;

namespace SistemaFacturacion.Controllers
{
    public class PosventaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PosventaController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Posventa - solicitudes sin alterar el historial de facturación.
        public async Task<IActionResult> Index(string? tipo, string? estado)
        {
            var consulta = _context.CasosPosventa
                .AsNoTracking()
                .Include(c => c.DetalleFactura)
                    .ThenInclude(d => d!.Factura)
                .Include(c => c.DetalleFactura)
                    .ThenInclude(d => d!.Producto)
                .AsQueryable();

            if (tipo == "Garantia" || tipo == "Devolucion")
                consulta = consulta.Where(c => c.Tipo == tipo);

            if (estado == "Abierto" || estado == "EnRevision" ||
                estado == "Resuelto" || estado == "Rechazado")
                consulta = consulta.Where(c => c.Estado == estado);

            ViewBag.Tipo = tipo;
            ViewBag.Estado = estado;
            return View(await consulta.OrderByDescending(c => c.FechaSolicitud).ToListAsync());
        }

        // GET: Posventa/Create?detalleId=1
        public async Task<IActionResult> Create(int detalleId)
        {
            var detalle = await BuscarDetalle(detalleId);
            if (detalle == null) return NotFound();

            if (detalle.Factura?.Estado != "Emitida")
            {
                TempData["Error"] = "No se pueden abrir solicitudes sobre una factura anulada.";
                return RedirectToAction("Details", "Facturas", new { id = detalle.IdFactura });
            }

            var modelo = new CrearCasoPosventaViewModel { IdDetalle = detalleId };
            CargarInformacion(modelo, detalle);
            return View(modelo);
        }

        // POST: Posventa/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CrearCasoPosventaViewModel modelo)
        {
            var detalle = await BuscarDetalle(modelo.IdDetalle);
            if (detalle == null) return NotFound();

            CargarInformacion(modelo, detalle);
            if (detalle.Factura?.Estado != "Emitida")
                ModelState.AddModelError("", "La factura está anulada y no admite nuevas solicitudes.");

            if (modelo.Cantidad > detalle.Cantidad)
                ModelState.AddModelError(nameof(modelo.Cantidad),
                    $"No puedes solicitar más de {detalle.Cantidad} unidades.");

            if (string.IsNullOrWhiteSpace(modelo.Motivo) ||
                modelo.Motivo.Trim().Length < 10)
                ModelState.AddModelError(nameof(modelo.Motivo),
                    "Explica el problema con al menos 10 caracteres útiles.");

            if (!ModelState.IsValid) return View(modelo);

            // Serializable protege el total de cantidades ante solicitudes
            // concurrentes sobre la misma línea y tipo.
            await using var transaccion =
                await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var facturaActiva = await _context.Facturas
                    .AsNoTracking()
                    .AnyAsync(f => f.IdFactura == detalle.IdFactura && f.Estado == "Emitida");

                if (!facturaActiva)
                {
                    await transaccion.RollbackAsync();
                    ModelState.AddModelError("", "La factura ya no está emitida.");
                    return View(modelo);
                }

                var solicitudes = _context.CasosPosventa
                    .Where(c => c.IdDetalle == modelo.IdDetalle && c.Tipo == modelo.Tipo);

                // Una devolución ya resuelta sigue contando: evita registrar
                // más unidades devueltas que las vendidas.
                // En garantía solo se reservan las cantidades de casos abiertos.
                if (modelo.Tipo == "Devolucion")
                    solicitudes = solicitudes.Where(c => c.Estado != "Rechazado");
                else
                    solicitudes = solicitudes.Where(c =>
                        c.Estado == "Abierto" || c.Estado == "EnRevision");

                var cantidadComprometida =
                    await solicitudes.SumAsync(c => (int?)c.Cantidad) ?? 0;

                if (cantidadComprometida + (long)modelo.Cantidad > detalle.Cantidad)
                {
                    await transaccion.RollbackAsync();
                    ModelState.AddModelError(nameof(modelo.Cantidad),
                        $"Esta línea tiene {detalle.Cantidad - cantidadComprometida} unidades disponibles para otra solicitud de ese tipo.");
                    return View(modelo);
                }

                _context.CasosPosventa.Add(new CasoPosventa
                {
                    IdDetalle = detalle.IdDetalle,
                    Tipo = modelo.Tipo,
                    Cantidad = modelo.Cantidad,
                    Motivo = modelo.Motivo.Trim(),
                    Estado = "Abierto",
                    FechaSolicitud = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaccion.CommitAsync();

                TempData["Mensaje"] =
                    "Solicitud de posventa registrada. No se ha realizado un reembolso ni ajustado el inventario.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await transaccion.RollbackAsync();
                throw;
            }
        }

        // GET: Posventa/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var caso = await _context.CasosPosventa
                .AsNoTracking()
                .Include(c => c.DetalleFactura)
                    .ThenInclude(d => d!.Factura)
                .Include(c => c.DetalleFactura)
                    .ThenInclude(d => d!.Producto)
                .FirstOrDefaultAsync(c => c.IdCaso == id);

            return caso == null ? NotFound() : View(caso);
        }

        // POST: Posventa/CambiarEstado/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(
            int id, string estado, string? resolucion, byte[]? rowVersion)
        {
            var caso = await _context.CasosPosventa.FirstOrDefaultAsync(c => c.IdCaso == id);
            if (caso == null) return NotFound();

            if (rowVersion == null || rowVersion.Length != 8)
            {
                TempData["Error"] = "Versión de solicitud inválida. Recarga la página.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var permitido =
                (caso.Estado == "Abierto" &&
                    (estado == "EnRevision" || estado == "Rechazado")) ||
                (caso.Estado == "EnRevision" &&
                    (estado == "Resuelto" || estado == "Rechazado"));

            if (!permitido)
            {
                TempData["Error"] = "La transición de estado no está permitida.";
                return RedirectToAction(nameof(Details), new { id });
            }

            resolucion = resolucion?.Trim();
            if (resolucion?.Length > 1000 ||
                (estado != "EnRevision" && string.IsNullOrWhiteSpace(resolucion)))
            {
                TempData["Error"] =
                    "Para cerrar el caso debes indicar una resolución de hasta 1000 caracteres.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.Entry(caso).Property(c => c.RowVersion).OriginalValue = rowVersion;
            caso.Estado = estado;
            caso.Resolucion = string.IsNullOrWhiteSpace(resolucion) ? caso.Resolucion : resolucion;
            caso.FechaActualizacion = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
                TempData["Mensaje"] =
                    "Estado actualizado. La resolución no cambia automáticamente cobros ni existencias.";
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["Error"] =
                    "Alguien actualizó el caso mientras lo tenías abierto. Recarga para ver los cambios.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<DetalleFactura?> BuscarDetalle(int detalleId)
        {
            return await _context.DetallesFactura
                .AsNoTracking()
                .Include(d => d.Factura)
                .Include(d => d.Producto)
                .FirstOrDefaultAsync(d => d.IdDetalle == detalleId);
        }

        private static void CargarInformacion(
            CrearCasoPosventaViewModel modelo, DetalleFactura detalle)
        {
            modelo.IdFactura = detalle.IdFactura;
            modelo.NumeroFactura = detalle.Factura?.NumeroFactura ?? "";
            modelo.NombreProducto = detalle.Producto?.Nombre ?? "Producto sin nombre";
            modelo.FechaVenta = detalle.Factura?.Fecha ?? DateTime.MinValue;
            modelo.CantidadVendida = detalle.Cantidad;
            modelo.GarantiaMeses = detalle.GarantiaMeses;
        }
    }
}

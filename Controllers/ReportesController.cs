using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;

namespace SistemaFacturacion.Controllers
{
    public class ReportesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportesController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            DateTime? desde,
            DateTime? hasta)
        {
            var fechaDesde =
                desde?.Date ?? DateTime.Today;

            var fechaHasta =
                hasta?.Date.AddDays(1)
                ?? DateTime.Today.AddDays(1);


            var facturas =
                await _context.Facturas
                .Include(f => f.Cliente)
                .Where(f =>
                    f.Fecha >= fechaDesde &&
                    f.Fecha < fechaHasta &&
                    f.Estado == "Emitida")
                .OrderByDescending(f => f.Fecha)
                .ToListAsync();


            ViewBag.Desde =
                fechaDesde.ToString("yyyy-MM-dd");

            ViewBag.Hasta =
                fechaHasta.AddDays(-1)
                    .ToString("yyyy-MM-dd");


            ViewBag.TotalVentas =
                facturas.Sum(f => f.Total);

            ViewBag.TotalFacturas =
                facturas.Count;

            ViewBag.TotalImpuestos =
                facturas.Sum(f => f.Impuesto);

            ViewBag.TotalDescuentos =
                facturas.Sum(f => f.Descuento);


            return View(facturas);
        }
    }
}
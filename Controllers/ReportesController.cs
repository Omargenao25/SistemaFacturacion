using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.ViewModels;

namespace SistemaFacturacion.Controllers
{
    public class ReportesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            DateTime? desde,
            DateTime? hasta)
        {
            // Si no se selecciona fecha,
            // utilizamos el día actual.
            var fechaDesde =
                desde?.Date ?? DateTime.Today;

            var fechaHasta =
                hasta?.Date.AddDays(1)
                ?? DateTime.Today.AddDays(1);


            // ============================
            // FACTURAS DEL PERÍODO
            // ============================

            var facturas = await _context.Facturas
                .Include(f => f.Cliente)
                .Where(f =>
                    f.Fecha >= fechaDesde &&
                    f.Fecha < fechaHasta &&
                    f.Estado == "Emitida")
                .OrderByDescending(f => f.Fecha)
                .ToListAsync();


            // ============================
            // RESUMEN DE VENTAS
            // ============================

            ViewBag.TotalVentas =
                facturas.Sum(f => f.Total);

            ViewBag.TotalFacturas =
                facturas.Count;

            ViewBag.TotalImpuestos =
                facturas.Sum(f => f.Impuesto);

            ViewBag.TotalDescuentos =
                facturas.Sum(f => f.Descuento);


            // ============================
            // PRODUCTOS MÁS VENDIDOS
            // ============================

            var productosMasVendidos =
                await _context.DetallesFactura

                .Where(d =>
                    d.Factura != null &&
                    d.Factura.Estado == "Emitida" &&
                    d.Factura.Fecha >= fechaDesde &&
                    d.Factura.Fecha < fechaHasta)

                .GroupBy(d => new
                {
                    d.IdProducto,
                    Nombre = d.Producto!.Nombre
                })

                .Select(g =>
                    new ProductoVendidoReporteViewModel
                    {
                        IdProducto =
                            g.Key.IdProducto,

                        Nombre =
                            g.Key.Nombre,

                        CantidadVendida =
                            g.Sum(x => x.Cantidad),

                        TotalVendido =
                            g.Sum(x => x.Subtotal)
                    })

                .OrderByDescending(
                    p => p.CantidadVendida)

                .Take(5)

                .ToListAsync();


            ViewBag.ProductosMasVendidos =
                productosMasVendidos;


            // ============================
            // PRODUCTOS CON BAJO STOCK
            // ============================

            var productosBajoStock =
                await _context.Productos

                .Include(p => p.Categoria)

                .Where(p =>
                    p.Estado &&
                    p.Stock <= 5)

                .OrderBy(p => p.Stock)

                .ToListAsync();


            ViewBag.ProductosBajoStock =
                productosBajoStock;


            // ============================
            // FECHAS PARA LA VISTA
            // ============================

            ViewBag.Desde =
                fechaDesde.ToString("yyyy-MM-dd");

            ViewBag.Hasta =
                fechaHasta.AddDays(-1)
                    .ToString("yyyy-MM-dd");

            var detallesPeriodo = await _context.DetallesFactura
                .Where(d =>
                d.Factura != null &&
               d.Factura.Estado == "Emitida" &&
               d.Factura.Fecha >= fechaDesde &&
               d.Factura.Fecha < fechaHasta)
                .ToListAsync();


            var costoTotal = detallesPeriodo.Sum(
                d => d.CostoUnitario * d.Cantidad
            );


            var ventaProductos = detallesPeriodo.Sum(
                d => d.Subtotal
            );


            var gananciaBruta =
                ventaProductos - costoTotal;


            ViewBag.CostoTotal = costoTotal;

            ViewBag.GananciaBruta = gananciaBruta;


            return View(facturas);
        }
    }
}
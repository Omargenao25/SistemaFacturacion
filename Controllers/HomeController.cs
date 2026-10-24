using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models;

namespace SistemaFacturacion.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalClientes =
                await _context.Clientes.CountAsync();

            ViewBag.TotalProductos =
                await _context.Productos.CountAsync();

            ViewBag.TotalFacturas =
                await _context.Facturas.CountAsync();


            var hoy = DateTime.Today;
            var manana = hoy.AddDays(1);

            ViewBag.VentasHoy =
                await _context.Facturas
                .Where(f =>
                    f.Fecha >= hoy &&
                    f.Fecha < manana &&
                    f.Estado == "Emitida")
                .SumAsync(f => (decimal?)f.Total)
                ?? 0;


            ViewBag.ProductosBajoStock =
                await _context.Productos
                .Where(p =>
                    p.Estado &&
                    p.Stock <= 5)
                .OrderBy(p => p.Stock)
                .Take(5)
                .ToListAsync();


            ViewBag.UltimasFacturas =
                await _context.Facturas
                .Include(f => f.Cliente)
                .OrderByDescending(f => f.Fecha)
                .Take(5)
                .ToListAsync();


            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(
                new ErrorViewModel
                {
                    RequestId =
                        Activity.Current?.Id
                        ?? HttpContext.TraceIdentifier
                });
        }
    }
}
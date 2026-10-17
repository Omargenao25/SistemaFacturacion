using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models;
using SistemaFacturacion.ViewModels;

namespace SistemaFacturacion.Controllers
{
    public class FacturasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FacturasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Facturas/Create
        public async Task<IActionResult> Create()
        {
            await CargarListas();

            var modelo = new CrearFacturaViewModel();

            modelo.Detalles.Add(new DetalleFacturaInput());

            return View(modelo);
        }

        // POST: Facturas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CrearFacturaViewModel modelo)
        {
            if (modelo.Detalles == null || modelo.Detalles.Count == 0)
            {
                ModelState.AddModelError(
                    "",
                    "Debe agregar al menos un producto."
                );
            }

            if (!ModelState.IsValid)
            {
                await CargarListas();
                return View(modelo);
            }

            // Si el mismo producto aparece varias veces,
            // juntamos las cantidades.
            var productosSolicitados = modelo.Detalles
                .GroupBy(x => x.IdProducto)
                .Select(x => new
                {
                    IdProducto = x.Key,
                    Cantidad = x.Sum(y => y.Cantidad)
                })
                .ToList();

            var idsProductos = productosSolicitados
                .Select(x => x.IdProducto)
                .ToList();

            var productos = await _context.Productos
                .Where(p => idsProductos.Contains(p.IdProducto))
                .ToDictionaryAsync(p => p.IdProducto);

            decimal subtotal = 0;

            foreach (var item in productosSolicitados)
            {
                if (!productos.TryGetValue(item.IdProducto, out var producto))
                {
                    ModelState.AddModelError(
                        "",
                        "Uno de los productos seleccionados no existe."
                    );

                    await CargarListas();
                    return View(modelo);
                }

                if (!producto.Estado)
                {
                    ModelState.AddModelError(
                        "",
                        $"El producto {producto.Nombre} está inactivo."
                    );

                    await CargarListas();
                    return View(modelo);
                }

                if (producto.Stock < item.Cantidad)
                {
                    ModelState.AddModelError(
                        "",
                        $"No hay suficiente existencia de {producto.Nombre}. Stock disponible: {producto.Stock}."
                    );

                    await CargarListas();
                    return View(modelo);
                }

                subtotal += producto.Precio * item.Cantidad;
            }

            if (modelo.Descuento > subtotal)
            {
                ModelState.AddModelError(
                    nameof(modelo.Descuento),
                    "El descuento no puede superar el subtotal."
                );

                await CargarListas();
                return View(modelo);
            }

            decimal impuesto = Math.Round(subtotal * 0.18m, 2);

            decimal total =
                subtotal + impuesto - modelo.Descuento;

            await using var transaccion =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var factura = new Factura
                {
                    NumeroFactura =
                        $"FAC-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",

                    IdCliente = modelo.IdCliente,
                    Fecha = DateTime.Now,
                    Subtotal = subtotal,
                    Impuesto = impuesto,
                    Descuento = modelo.Descuento,
                    Total = total,
                    Estado = "Emitida"
                };

                _context.Facturas.Add(factura);

                await _context.SaveChangesAsync();

                foreach (var item in productosSolicitados)
                {
                    var producto = productos[item.IdProducto];

                    var detalle = new DetalleFactura
                    {
                        IdFactura = factura.IdFactura,
                        IdProducto = producto.IdProducto,
                        Cantidad = item.Cantidad,
                        Precio = producto.Precio,
                        CostoUnitario = producto.Costo,
                        Subtotal = producto.Precio * item.Cantidad
                    };

                    _context.DetallesFactura.Add(detalle);

                    // Descontar inventario
                    producto.Stock -= item.Cantidad;
                }

                var pago = new Pago
                {
                    IdFactura = factura.IdFactura,
                    MetodoPago = modelo.MetodoPago,
                    Monto = total,
                    Fecha = DateTime.Now
                };

                _context.Pagos.Add(pago);

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

                return RedirectToAction(
                    nameof(Details),
                    new { id = factura.IdFactura }
                );
            }
            catch
            {
                await transaccion.RollbackAsync();
                throw;
            }
        }

        private async Task CargarListas()
        {
            var clientes = await _context.Clientes
                .OrderBy(c => c.Nombre)
                .Select(c => new
                {
                    c.IdCliente,
                    NombreCompleto = c.Nombre + " " + c.Apellido
                })
                .ToListAsync();

            ViewBag.Clientes = new SelectList(
                clientes,
                "IdCliente",
                "NombreCompleto"
            );

            ViewBag.Productos = await _context.Productos
                .Where(p => p.Estado && p.Stock > 0)
                .OrderBy(p => p.Nombre)
                .ToListAsync();
        }

        public async Task<IActionResult> Index(
     string? buscar,
     string? estado,
     DateTime? desde,
     DateTime? hasta)
        {
            var consulta = _context.Facturas
                .Include(f => f.Cliente)
                .AsQueryable();


            // BUSCAR POR NÚMERO DE FACTURA O CLIENTE
            if (!string.IsNullOrWhiteSpace(buscar))
            {
                buscar = buscar.Trim();

                consulta = consulta.Where(f =>
                    f.NumeroFactura.Contains(buscar) ||
                    (
                        f.Cliente != null &&
                        (
                            f.Cliente.Nombre.Contains(buscar) ||
                            f.Cliente.Apellido.Contains(buscar) ||
                            (f.Cliente.Nombre + " " + f.Cliente.Apellido)
                                .Contains(buscar)
                        )
                    )
                );
            }


            // FILTRAR POR ESTADO
            if (!string.IsNullOrWhiteSpace(estado) &&
                estado != "Todos")
            {
                consulta = consulta.Where(
                    f => f.Estado == estado
                );
            }


            // FILTRAR DESDE FECHA
            if (desde.HasValue)
            {
                var fechaDesde = desde.Value.Date;

                consulta = consulta.Where(
                    f => f.Fecha >= fechaDesde
                );
            }


            // FILTRAR HASTA FECHA
            if (hasta.HasValue)
            {
                var fechaHasta =
                    hasta.Value.Date.AddDays(1);

                consulta = consulta.Where(
                    f => f.Fecha < fechaHasta
                );
            }


            var facturas = await consulta
                .OrderByDescending(f => f.Fecha)
                .ToListAsync();


            // Mantener los filtros escritos
            ViewBag.Buscar = buscar;
            ViewBag.Estado = estado;
            ViewBag.Desde =
                desde?.ToString("yyyy-MM-dd");

            ViewBag.Hasta =
                hasta?.ToString("yyyy-MM-dd");


            return View(facturas);
        }

        // Temporalmente dejamos preparado Details.
        public async Task<IActionResult> Details(int id)
        {
            var factura = await _context.Facturas
                .Include(f => f.Cliente)
                .Include(f => f.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(f => f.IdFactura == id);

            if (factura == null)
            {
                return NotFound();
            }

            var pago = await _context.Pagos
                .FirstOrDefaultAsync(p => p.IdFactura == id);

            ViewBag.Pago = pago;

            return View(factura);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Anular(int id)
        {
            var factura = await _context.Facturas
                .Include(f => f.Detalles)
                .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(f => f.IdFactura == id);

            if (factura == null)
            {
                return NotFound();
            }

            if (factura.Estado == "Anulada")
            {
                TempData["Mensaje"] = "La factura ya se encuentra anulada.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = factura.IdFactura }
                );
            }

            await using var transaccion =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // Devolver los productos al inventario
                foreach (var detalle in factura.Detalles)
                {
                    if (detalle.Producto != null)
                    {
                        detalle.Producto.Stock += detalle.Cantidad;
                    }
                }

                // Cambiar estado
                factura.Estado = "Anulada";

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

                TempData["Mensaje"] =
                    "La factura fue anulada correctamente y el inventario fue actualizado.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = factura.IdFactura }
                );
            }
            catch
            {
                await transaccion.RollbackAsync();
                throw;
            }
        }
    }
}
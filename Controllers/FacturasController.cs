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
        // Política comercial interna: 7 días exactos desde la emisión.
        private const int PlazoAnulacionDias = 7;

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

            // Solo se acepta un cliente visible para la empresa actual;
            // evita referencias a clientes de otra empresa mediante POST.
            if (!await _context.Clientes.AsNoTracking()
                .AnyAsync(c => c.IdCliente == modelo.IdCliente))
            {
                ModelState.AddModelError(
                    nameof(modelo.IdCliente),
                    "Selecciona un cliente válido de la empresa.");
            }

            // El model binder puede entregar Detalles nulo: no continuar.
            if (!ModelState.IsValid || modelo.Detalles == null)
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
                // Orden fijo para disminuir conflictos entre ventas concurrentes.
                .OrderBy(x => x.IdProducto)
                .ToList();

            var idsProductos = productosSolicitados
                .Select(x => x.IdProducto)
                .ToList();

            var productos = await _context.Productos
                .AsNoTracking()
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
                // Reservar existencias de manera atómica dentro de la transacción.
                // El WHERE se vuelve a evaluar en SQL Server al efectuar cada UPDATE:
                // dos ventas simultáneas no pueden descontar la misma unidad.
                foreach (var item in productosSolicitados)
                {
                    var producto = productos[item.IdProducto];
                    var versionAlCotizar = producto.RowVersion;

                    var filasActualizadas = await _context.Productos
                        .Where(p =>
                            p.IdProducto == item.IdProducto &&
                            p.Estado &&
                            p.RowVersion == versionAlCotizar &&
                            p.Stock >= item.Cantidad)
                        .ExecuteUpdateAsync(cambios => cambios
                            .SetProperty(p => p.Stock, p => p.Stock - item.Cantidad));

                    if (filasActualizadas != 1)
                    {
                        await transaccion.RollbackAsync();

                        ModelState.AddModelError(
                            "",
                            $"El inventario de {producto.Nombre} cambió mientras se procesaba la venta. " +
                            "No se generó la factura. Verifica las existencias e inténtalo de nuevo.");

                        await CargarListas();
                        return View(modelo);
                    }
                }

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
                        GarantiaMeses = producto.GarantiaMeses,
                        Precio = producto.Precio,
                        CostoUnitario = producto.Costo,
                        Subtotal = producto.Precio * item.Cantidad
                    };

                    _context.DetallesFactura.Add(detalle);
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
            ViewBag.Reembolso = await _context.Reembolsos
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.IdFactura == id);
            ViewBag.FechaLimiteAnulacion = factura.Fecha.AddDays(PlazoAnulacionDias);
            ViewBag.PuedeAnular = factura.Estado == "Emitida" &&
                DateTime.Now <= factura.Fecha.AddDays(PlazoAnulacionDias);

            return View(factura);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Anular(int id)
        {
            var factura = await _context.Facturas
                .AsNoTracking()
                .Include(f => f.Detalles)
                .FirstOrDefaultAsync(f => f.IdFactura == id);

            if (factura == null)
            {
                return NotFound();
            }

            if (factura.Estado == "Anulada")
            {
                TempData["Mensaje"] = "La factura ya se encuentra anulada.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // La restricción se comprueba en el servidor, aunque el usuario
            // envíe el POST directamente y no desde el botón de la vista.
            var ahora = DateTime.Now;
            if (ahora > factura.Fecha.AddDays(PlazoAnulacionDias))
            {
                TempData["Error"] =
                    "Se agotó el plazo interno de 7 días para anular esta factura. " +
                    "Las devoluciones o garantías deben gestionarse por separado.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await using var transaccion =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // Solo una solicitud puede cambiar esta factura de Emitida
                // a Anulada. Las siguientes no devolverán el stock otra vez.
                // Revalidación atómica: dos solicitudes no pueden anular la
                // misma factura, ni saltarse el plazo con un POST manual.
                var fechaLimiteInferior = DateTime.Now.AddDays(-PlazoAnulacionDias);
                var filasActualizadas = await _context.Facturas
                    .Where(f =>
                        f.IdFactura == id &&
                        f.Estado == "Emitida" &&
                        f.Fecha >= fechaLimiteInferior)
                    .ExecuteUpdateAsync(cambios => cambios
                        .SetProperty(f => f.Estado, "Anulada"));

                if (filasActualizadas != 1)
                {
                    await transaccion.RollbackAsync();
                    TempData["Error"] =
                        "La factura ya fue anulada o superó los 7 días permitidos. " +
                        "No se modificó el inventario.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // El dinero NO se mueve aquí. Capturamos los pagos originales
                // bajo la misma transacción que la anulación y la reposición.
                var pagosRegistrados = await _context.Pagos
                    .AsNoTracking()
                    .Where(p => p.IdFactura == id)
                    .Select(p => new { p.Monto, p.MetodoPago })
                    .ToListAsync();

                if (pagosRegistrados.Any(p => p.Monto < 0))
                {
                    await transaccion.RollbackAsync();
                    TempData["Error"] =
                        "La factura contiene un pago negativo que requiere revisión. " +
                        "No se anuló ni se modificó el inventario.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                decimal montoCobrado = pagosRegistrados.Sum(p => p.Monto);
                var metodos = pagosRegistrados
                    .Select(p => p.MetodoPago)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                string metodoOriginal = metodos.Count switch
                {
                    0 => "SinRegistro",
                    1 => metodos[0],
                    _ => "Mixto"
                };

                // Un cobro faltante, mixto o de importe diferente no se
                // presume conciliado: requiere una revisión administrativa.
                bool requiereRevision = montoCobrado <= 0 ||
                    montoCobrado != factura.Total ||
                    metodos.Count != 1 ||
                    pagosRegistrados.Any(p => p.Monto <= 0);

                // Incrementos atómicos que no sobrescriben ventas simultáneas.
                foreach (var detalle in factura.Detalles)
                {
                    var productosActualizados = await _context.Productos
                        .Where(p => p.IdProducto == detalle.IdProducto)
                        .ExecuteUpdateAsync(cambios => cambios
                            .SetProperty(p => p.Stock, p => p.Stock + detalle.Cantidad));

                    if (productosActualizados != 1)
                    {
                        throw new InvalidOperationException(
                            "No se pudo recuperar un producto vinculado a la factura. " +
                            "La anulación completa fue revertida.");
                    }
                }

                // Una única solicitud por factura. El índice único
                // (IdEmpresa, IdFactura) también bloquea duplicados en SQL.
                _context.Reembolsos.Add(new Reembolso
                {
                    IdFactura = factura.IdFactura,
                    MontoSolicitado = montoCobrado,
                    MetodoPagoOriginal = metodoOriginal,
                    Estado = requiereRevision ? "PendienteRevision" : "Pendiente",
                    FechaSolicitudUtc = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                await transaccion.CommitAsync();

                TempData["Mensaje"] = requiereRevision
                    ? "Factura anulada e inventario restaurado. Se creó una solicitud " +
                      "financiera pendiente de revisión; no se devolvió dinero automáticamente."
                    : "Factura anulada e inventario restaurado. Se creó un reembolso " +
                      "pendiente de confirmación; no se devolvió dinero automáticamente.";

                return RedirectToAction(nameof(Details), new { id });
            }
            catch
            {
                await transaccion.RollbackAsync();
                throw;
            }
        }
    }
}
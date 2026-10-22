using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.ViewModels;

namespace SistemaFacturacion.Controllers
{
    /// <summary>
    /// Seguimiento operativo de facturas anuladas: SOLO GET.
    /// No confirma reembolsos, no registra recepción y no cambia inventario.
    /// El filtro tenant se aplica a cada DbSet comercial consultado.
    /// </summary>
    public sealed class ControlAnulacionesController : Controller
    {
        private const int TamanoPagina = 25;
        private readonly ApplicationDbContext _context;

        public ControlAnulacionesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? buscar, int pagina = 1)
        {
            buscar = buscar?.Trim() ?? string.Empty;
            if (buscar.Length > 80)
                buscar = buscar[..80];

            var consulta = _context.Facturas.AsNoTracking()
                .Where(f => f.Estado == "Anulada");

            if (buscar.Length > 0)
            {
                consulta = consulta.Where(f =>
                    f.NumeroFactura.Contains(buscar) ||
                    (f.Cliente != null &&
                     (f.Cliente.Nombre.Contains(buscar) ||
                      f.Cliente.Apellido.Contains(buscar))));
            }

            var total = await consulta.CountAsync();
            var totalPaginas = total == 0 ? 1 : (total - 1) / TamanoPagina + 1;
            pagina = Math.Clamp(pagina, 1, totalPaginas);

            // Paginar ANTES de consultar relaciones, para no multiplicar
            // filas ni dinero cuando una factura contiene varios productos.
            var facturas = await consulta
                .Include(f => f.Cliente)
                .OrderByDescending(f => f.Fecha)
                .ThenByDescending(f => f.IdFactura)
                .Skip((pagina - 1) * TamanoPagina)
                .Take(TamanoPagina)
                .ToListAsync();

            var resultado = new ControlAnulacionesViewModel
            {
                Buscar = buscar,
                Pagina = pagina,
                TotalPaginas = totalPaginas,
                TotalFacturas = total
            };

            if (facturas.Count == 0)
                return View(resultado);

            var ids = facturas.Select(f => f.IdFactura).ToList();

            // Consulta de solicitudes por factura. Existe un índice único
            // (IdEmpresa, IdFactura), así que jamás sumar por línea vendida.
            var reembolsos = await _context.Reembolsos.AsNoTracking()
                .Where(r => ids.Contains(r.IdFactura))
                .Select(r => new
                {
                    r.IdFactura,
                    r.Estado,
                    r.MontoSolicitado,
                    r.MetodoPagoOriginal
                })
                .ToDictionaryAsync(r => r.IdFactura);

            // Cuántas líneas deben tener una obligación física.
            // Las anulaciones antiguas pueden no tener registros de recepción.
            var lineas = await _context.DetallesFactura.AsNoTracking()
                .Where(d => ids.Contains(d.IdFactura))
                .GroupBy(d => d.IdFactura)
                .Select(g => new { IdFactura = g.Key, Total = g.Count() })
                .ToDictionaryAsync(g => g.IdFactura, g => g.Total);

            var recepciones = await _context.RecepcionesAnulacion.AsNoTracking()
                .Where(r => r.DetalleFactura != null &&
                            ids.Contains(r.DetalleFactura.IdFactura))
                .Select(r => new
                {
                    IdFactura = r.DetalleFactura!.IdFactura,
                    r.Estado,
                    r.CantidadEsperada,
                    r.CantidadRecibida
                })
                .ToListAsync();

            var recepcionesPorFactura = recepciones
                .GroupBy(r => r.IdFactura)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var factura in facturas)
            {
                var item = new ControlAnulacionItem
                {
                    IdFactura = factura.IdFactura,
                    NumeroFactura = factura.NumeroFactura,
                    Cliente = factura.Cliente == null
                        ? "Cliente no disponible"
                        : $"{factura.Cliente.Nombre} {factura.Cliente.Apellido}",
                    Fecha = factura.Fecha,
                    TotalFactura = factura.Total,
                    LineasFactura = lineas.TryGetValue(factura.IdFactura, out var n)
                        ? n : 0
                };

                if (reembolsos.TryGetValue(factura.IdFactura, out var reembolso))
                {
                    item.TieneReembolso = true;
                    item.EstadoReembolso = reembolso.Estado;
                    item.MontoSolicitado = reembolso.MontoSolicitado;
                    item.MetodoPagoOriginal = reembolso.MetodoPagoOriginal;
                }

                if (recepcionesPorFactura.TryGetValue(factura.IdFactura, out var registros))
                {
                    item.RegistrosRecepcion = registros.Count;
                    if (item.LineasFactura > 0 &&
                        registros.Count == item.LineasFactura)
                    {
                        item.UnidadesPendientes = registros.Sum(r =>
                            Math.Max(0, r.CantidadEsperada - r.CantidadRecibida));

                        item.EstadoMercancia = registros.All(r => r.Estado == "Completada")
                            ? "Completada"
                            : item.UnidadesPendientes > 0 ? "Pendiente" : "En seguimiento";
                    }
                    else
                    {
                        item.EstadoMercancia = "Revisar registros";
                    }
                }

                resultado.Facturas.Add(item);
            }

            // Nunca presentar estos KPIs como totales globales; corresponden
            // únicamente a las 25 facturas visibles en ESTA página.
            resultado.FacturasConSolicitudAbierta =
                resultado.Facturas.Count(f => f.SolicitudFinancieraAbierta);
            resultado.MontoSolicitudesAbiertas =
                resultado.Facturas.Where(f => f.SolicitudFinancieraAbierta)
                    .Sum(f => f.MontoSolicitado ?? 0m);
            resultado.UnidadesPorRecibirRegistradas =
                resultado.Facturas.Sum(f => f.UnidadesPendientes ?? 0);
            resultado.FacturasConDatosPorConciliar =
                resultado.Facturas.Count(f => f.NecesitaConciliacion);

            return View(resultado);
        }
    }
}

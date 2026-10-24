using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models;

namespace SistemaFacturacion.Controllers
{
    [Authorize]
    public class ClientesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClientesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Clientes
        public async Task<IActionResult> Index()
        {
            return View(await _context.Clientes.ToListAsync());
        }

        // GET: Clientes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(m => m.IdCliente == id);
            if (cliente == null)
            {
                return NotFound();
            }

            return View(cliente);
        }

        // GET: Clientes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Clientes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdCliente,Nombre,Apellido,Telefono,Correo,Direccion,IdentificacionFiscal")] Cliente cliente)
        {
            if (ModelState.IsValid)
            {
                _context.Add(cliente);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(cliente);
        }

        // GET: Clientes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cliente = await _context.Clientes.FirstOrDefaultAsync(e => e.IdCliente == id);
            if (cliente == null)
            {
                return NotFound();
            }
            return View(cliente);
        }

        // POST: Clientes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdCliente,Nombre,Apellido,Telefono,Correo,Direccion,IdentificacionFiscal")] Cliente cliente)
        {
            if (id != cliente.IdCliente)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Se carga el registro dentro del tenant filtrado por EF.
                    // Nunca actualizar directamente un objeto enviado por POST.
                    var actual = await _context.Clientes
                        .FirstOrDefaultAsync(e => e.IdCliente == id);
                    if (actual == null)
                        return NotFound();

                    actual.Nombre = cliente.Nombre;
                    actual.Apellido = cliente.Apellido;
                    actual.Telefono = cliente.Telefono;
                    actual.Correo = cliente.Correo;
                    actual.Direccion = cliente.Direccion;
                    actual.IdentificacionFiscal = cliente.IdentificacionFiscal;
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ClienteExists(cliente.IdCliente))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(cliente);
        }

        // GET: Clientes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(m => m.IdCliente == id);
            if (cliente == null)
            {
                return NotFound();
            }

            // Previene borrados de registros con información histórica asociada.
            ViewBag.Bloqueado = await _context.Facturas
                .AsNoTracking()
                .AnyAsync(x => x.IdCliente == id);

            return View(cliente);
        }

        // POST: Clientes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var cliente = await _context.Clientes.FirstOrDefaultAsync(e => e.IdCliente == id);

            if (cliente == null)
            {
                return NotFound();
            }

            // La comprobación se repite al confirmar, no solo al abrir la pantalla.
            var tieneRelaciones = await _context.Facturas
                .AsNoTracking()
                .AnyAsync(x => x.IdCliente == id);

            if (tieneRelaciones)
            {
                TempData["Error"] = "Este cliente tiene facturas registradas, por lo que no puede eliminarse. Conserva el registro para mantener el historial.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            try
            {
                _context.Clientes.Remove(cliente);
                await _context.SaveChangesAsync();

                TempData["Mensaje"] = "Cliente eliminado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                // Las FK NO ACTION refuerzan el control ante relaciones nuevas
                // o cambios concurrentes entre la consulta y el borrado.
                TempData["Error"] = "No se pudo eliminar el registro. Puede tener información relacionada o existir una restricción de base de datos.";
                return RedirectToAction(nameof(Delete), new { id });
            }
        }

        private bool ClienteExists(int id)
        {
            return _context.Clientes.Any(e => e.IdCliente == id);
        }
    }
}

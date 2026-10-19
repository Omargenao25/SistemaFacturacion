using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models;

namespace SistemaFacturacion.Controllers
{
    public class ProductosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Productos
        public async Task<IActionResult> Index()
        {
            return View(await _context.Productos.ToListAsync());
        }

        // GET: Productos/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var producto = await _context.Productos
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(m => m.IdProducto == id);
            if (producto == null)
            {
                return NotFound();
            }

            return View(producto);
        }

        // GET: Productos/Create
        public async Task<IActionResult> Create()
        {
            await CargarCategorias();
            return View();
        }

        // POST: Productos/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdProducto,Codigo,Nombre,Descripcion,Precio,Costo,Stock,Estado,IdCategoria")] Producto producto)
        {
            if (!await _context.Categorias.AnyAsync(c => c.IdCategoria == producto.IdCategoria))
            {
                ModelState.AddModelError(nameof(Producto.IdCategoria), "Selecciona una categoría válida.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(producto);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            await CargarCategorias(producto.IdCategoria);
            return View(producto);
        }

        // GET: Productos/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
            {
                return NotFound();
            }

            await CargarCategorias(producto.IdCategoria);
            return View(producto);
        }

        // POST: Productos/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdProducto,Codigo,Nombre,Descripcion,Precio,Costo,Stock,Estado,IdCategoria")] Producto producto)
        {
            if (id != producto.IdProducto)
            {
                return NotFound();
            }

            if (!await _context.Categorias.AnyAsync(c => c.IdCategoria == producto.IdCategoria))
            {
                ModelState.AddModelError(nameof(Producto.IdCategoria), "Selecciona una categoría válida.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(producto);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductoExists(producto.IdProducto))
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

            await CargarCategorias(producto.IdCategoria);
            return View(producto);
        }

        // GET: Productos/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var producto = await _context.Productos
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(m => m.IdProducto == id);
            if (producto == null)
            {
                return NotFound();
            }

            // Previene borrados de registros con información histórica asociada.
            ViewBag.Bloqueado = await _context.DetallesFactura
                .AsNoTracking()
                .AnyAsync(x => x.IdProducto == id);

            return View(producto);
        }

        // POST: Productos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var producto = await _context.Productos.FindAsync(id);

            if (producto == null)
            {
                return NotFound();
            }

            // La comprobación se repite al confirmar, no solo al abrir la pantalla.
            var tieneRelaciones = await _context.DetallesFactura
                .AsNoTracking()
                .AnyAsync(x => x.IdProducto == id);

            if (tieneRelaciones)
            {
                TempData["Error"] = "Este producto aparece en facturas anteriores (incluso anuladas) y no puede eliminarse. Puedes desactivarlo desde Editar.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            try
            {
                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync();

                TempData["Mensaje"] = "Producto eliminado correctamente.";
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

        private async Task CargarCategorias(int? seleccionada = null)
        {
            var categorias = await _context.Categorias
                .AsNoTracking()
                .OrderBy(c => c.Nombre)
                .Select(c => new { c.IdCategoria, c.Nombre })
                .ToListAsync();

            ViewBag.Categorias = new SelectList(categorias, "IdCategoria", "Nombre", seleccionada);
        }

        private bool ProductoExists(int id)
        {
            return _context.Productos.Any(e => e.IdProducto == id);
        }
    }
}

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
    public class CategoriasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoriasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Categorias
        public async Task<IActionResult> Index()
        {
            return View(await _context.Categorias.ToListAsync());
        }

        // GET: Categorias/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var categoria = await _context.Categorias
                .FirstOrDefaultAsync(m => m.IdCategoria == id);
            if (categoria == null)
            {
                return NotFound();
            }

            return View(categoria);
        }

        // GET: Categorias/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Categorias/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdCategoria,Nombre,Descripcion")] Categoria categoria)
        {
            if (ModelState.IsValid)
            {
                _context.Add(categoria);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(categoria);
        }

        // GET: Categorias/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var categoria = await _context.Categorias.FirstOrDefaultAsync(e => e.IdCategoria == id);
            if (categoria == null)
            {
                return NotFound();
            }
            return View(categoria);
        }

        // POST: Categorias/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdCategoria,Nombre,Descripcion")] Categoria categoria)
        {
            if (id != categoria.IdCategoria)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Se carga el registro dentro del tenant filtrado por EF.
                    // Nunca actualizar directamente un objeto enviado por POST.
                    var actual = await _context.Categorias
                        .FirstOrDefaultAsync(e => e.IdCategoria == id);
                    if (actual == null)
                        return NotFound();

                    actual.Nombre = categoria.Nombre;
                    actual.Descripcion = categoria.Descripcion;
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoriaExists(categoria.IdCategoria))
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
            return View(categoria);
        }

        // GET: Categorias/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var categoria = await _context.Categorias
                .FirstOrDefaultAsync(m => m.IdCategoria == id);
            if (categoria == null)
            {
                return NotFound();
            }

            // Previene borrados de registros con información histórica asociada.
            ViewBag.Bloqueado = await _context.Productos
                .AsNoTracking()
                .AnyAsync(x => x.IdCategoria == id);

            return View(categoria);
        }

        // POST: Categorias/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var categoria = await _context.Categorias.FirstOrDefaultAsync(e => e.IdCategoria == id);

            if (categoria == null)
            {
                return NotFound();
            }

            // La comprobación se repite al confirmar, no solo al abrir la pantalla.
            var tieneRelaciones = await _context.Productos
                .AsNoTracking()
                .AnyAsync(x => x.IdCategoria == id);

            if (tieneRelaciones)
            {
                TempData["Error"] = "Esta categoría tiene productos asociados. Reasigna o retira los productos antes de eliminarla.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            try
            {
                _context.Categorias.Remove(categoria);
                await _context.SaveChangesAsync();

                TempData["Mensaje"] = "Categoría eliminada correctamente.";
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

        private bool CategoriaExists(int id)
        {
            return _context.Categorias.Any(e => e.IdCategoria == id);
        }
    }
}

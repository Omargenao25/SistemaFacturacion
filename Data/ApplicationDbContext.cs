using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Models;

namespace SistemaFacturacion.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Factura> Facturas { get; set; }
        public DbSet<DetalleFactura> DetallesFactura { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        public DbSet<CasoPosventa> CasosPosventa { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Protege el historial comercial: los registros relacionados
            // nunca se borran automáticamente por cascada.
            modelBuilder.Entity<Factura>()
                .HasOne(f => f.Cliente)
                .WithMany()
                .HasForeignKey(f => f.IdCliente)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Categoria)
                .WithMany()
                .HasForeignKey(p => p.IdCategoria)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<DetalleFactura>()
                .HasOne(d => d.Producto)
                .WithMany()
                .HasForeignKey(d => d.IdProducto)
                .OnDelete(DeleteBehavior.NoAction);

            // Nunca eliminar en cascada los casos asociados a una venta.
            modelBuilder.Entity<CasoPosventa>()
                .HasOne(c => c.DetalleFactura)
                .WithMany()
                .HasForeignKey(c => c.IdDetalle)
                .OnDelete(DeleteBehavior.NoAction);

            // También permite comprobar cantidades de solicitudes por artículo.
            modelBuilder.Entity<CasoPosventa>()
                .HasIndex(c => new { c.IdDetalle, c.Tipo, c.Estado });

            // Factura -> Detalles/Pagos conserva su configuración existente.
        }

    }
}
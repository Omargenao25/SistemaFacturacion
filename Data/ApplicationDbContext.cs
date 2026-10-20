using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Models;
using SistemaFacturacion.Models.SaaS;

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

        // Plataforma SaaS: aún no se utiliza para separar los datos comerciales.
        public DbSet<Empresa> Empresas { get; set; }
        public DbSet<EmpresaUsuario> EmpresaUsuarios { get; set; }

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

            // Registros globales de la plataforma. No activan acceso a datos
            // comerciales hasta completar el aislamiento por tenant.
            modelBuilder.Entity<Empresa>(entidad =>
            {
                entidad.ToTable("Empresas", tabla =>
                    tabla.HasCheckConstraint("CK_Empresas_Estado",
                        "[Estado] IN (N'PendienteConfiguracion', N'Activa', N'Suspendida', N'Archivada')"));
                entidad.HasKey(e => e.IdEmpresa);
                entidad.Property(e => e.IdEmpresa).ValueGeneratedNever();
                entidad.HasIndex(e => e.SlugNormalizado).IsUnique();
            });

            modelBuilder.Entity<EmpresaUsuario>(entidad =>
            {
                entidad.ToTable("EmpresaUsuarios", tabla =>
                {
                    tabla.HasCheckConstraint("CK_EmpresaUsuarios_Estado",
                        "[Estado] IN (N'Invitado', N'Activo', N'Suspendido')");
                    tabla.HasCheckConstraint("CK_EmpresaUsuarios_Rol",
                        "[Rol] IN (N'Propietario', N'Administrador', N'Cajero')");
                });
                entidad.HasKey(m => new { m.IdEmpresa, m.UserId });
                entidad.HasOne(m => m.Empresa)
                    .WithMany()
                    .HasForeignKey(m => m.IdEmpresa)
                    .OnDelete(DeleteBehavior.NoAction);
                entidad.HasOne(m => m.Usuario)
                    .WithMany()
                    .HasForeignKey(m => m.UserId)
                    .OnDelete(DeleteBehavior.NoAction);
                entidad.HasIndex(m => m.UserId);
            });

            // Fase 1B-1: todos los registros comerciales se relacionan con
            // la empresa heredada. Estos defaults son TEMPORALES y deberán
            // retirarse antes de admitir la segunda empresa / activar tenants.
            ConfigurarEmpresaInicial<Cliente>(modelBuilder);
            ConfigurarEmpresaInicial<Categoria>(modelBuilder);
            ConfigurarEmpresaInicial<Producto>(modelBuilder);
            ConfigurarEmpresaInicial<Factura>(modelBuilder);
            ConfigurarEmpresaInicial<DetalleFactura>(modelBuilder);
            ConfigurarEmpresaInicial<Pago>(modelBuilder);
            ConfigurarEmpresaInicial<CasoPosventa>(modelBuilder);

            // Factura -> Detalles/Pagos conserva su configuración existente.
        }

        private static void ConfigurarEmpresaInicial<T>(ModelBuilder modelBuilder)
            where T : class
        {
            modelBuilder.Entity<T>()
                .Property<Guid>("IdEmpresa")
                .HasDefaultValue(CompatibilidadEmpresaInicial.Id);

            modelBuilder.Entity<T>()
                .HasOne<Empresa>()
                .WithMany()
                .HasForeignKey("IdEmpresa")
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<T>()
                .HasIndex("IdEmpresa");
        }

    }
}
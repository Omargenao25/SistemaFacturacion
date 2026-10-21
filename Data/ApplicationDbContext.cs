using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Models;
using SistemaFacturacion.Models.SaaS;
using SistemaFacturacion.Services.Tenancy;

namespace SistemaFacturacion.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        private readonly IContextoEmpresa _contextoEmpresa;

        // Es una propiedad del contexto, no un valor congelado en el modelo EF.
        // Si no existe una empresa verificada, ninguna fila comercial es visible.
        private Guid IdEmpresaActual => _contextoEmpresa.IdEmpresa;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IContextoEmpresa contextoEmpresa)
            : base(options)
        {
            _contextoEmpresa = contextoEmpresa;
        }

        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Factura> Facturas { get; set; }
        public DbSet<DetalleFactura> DetallesFactura { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        public DbSet<CasoPosventa> CasosPosventa { get; set; }
        public DbSet<Reembolso> Reembolsos { get; set; }

        // Plataforma SaaS: aún no se utiliza para separar los datos comerciales.
        public DbSet<Empresa> Empresas { get; set; }
        public DbSet<EmpresaUsuario> EmpresaUsuarios { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Aislamiento a nivel SQL: toda referencia comercial incluye
            // IdEmpresa tanto en la fila hija como en la fila principal.
            // SQL Server rechaza asociaciones entre empresas diferentes.
            modelBuilder.Entity<Categoria>()
                .HasAlternateKey(c => new { c.IdEmpresa, c.IdCategoria });
            modelBuilder.Entity<Cliente>()
                .HasAlternateKey(c => new { c.IdEmpresa, c.IdCliente });
            modelBuilder.Entity<Producto>()
                .HasAlternateKey(p => new { p.IdEmpresa, p.IdProducto });
            modelBuilder.Entity<Factura>()
                .HasAlternateKey(f => new { f.IdEmpresa, f.IdFactura });
            modelBuilder.Entity<DetalleFactura>()
                .HasAlternateKey(d => new { d.IdEmpresa, d.IdDetalle });

            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Categoria)
                .WithMany()
                .HasForeignKey(p => new { p.IdEmpresa, p.IdCategoria })
                .HasPrincipalKey(c => new { c.IdEmpresa, c.IdCategoria })
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Factura>()
                .HasOne(f => f.Cliente)
                .WithMany()
                .HasForeignKey(f => new { f.IdEmpresa, f.IdCliente })
                .HasPrincipalKey(c => new { c.IdEmpresa, c.IdCliente })
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<DetalleFactura>()
                .HasOne(d => d.Factura)
                .WithMany(f => f.Detalles)
                .HasForeignKey(d => new { d.IdEmpresa, d.IdFactura })
                .HasPrincipalKey(f => new { f.IdEmpresa, f.IdFactura })
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<DetalleFactura>()
                .HasOne(d => d.Producto)
                .WithMany()
                .HasForeignKey(d => new { d.IdEmpresa, d.IdProducto })
                .HasPrincipalKey(p => new { p.IdEmpresa, p.IdProducto })
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Pago>()
                .HasOne(p => p.Factura)
                .WithMany()
                .HasForeignKey(p => new { p.IdEmpresa, p.IdFactura })
                .HasPrincipalKey(f => new { f.IdEmpresa, f.IdFactura })
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<CasoPosventa>()
                .HasOne(c => c.DetalleFactura)
                .WithMany()
                .HasForeignKey(c => new { c.IdEmpresa, c.IdDetalle })
                .HasPrincipalKey(d => new { d.IdEmpresa, d.IdDetalle })
                .OnDelete(DeleteBehavior.NoAction);

            // Conservar los índices de una columna de migraciones anteriores:
            // además se crean índices compuestos para las nuevas relaciones.
            modelBuilder.Entity<Producto>().HasIndex(p => p.IdCategoria);
            modelBuilder.Entity<Factura>().HasIndex(f => f.IdCliente);
            modelBuilder.Entity<DetalleFactura>().HasIndex(d => d.IdFactura);
            modelBuilder.Entity<DetalleFactura>().HasIndex(d => d.IdProducto);
            modelBuilder.Entity<Pago>().HasIndex(p => p.IdFactura);
            // CasosPosventa ya conserva el índice (IdDetalle, Tipo, Estado)
            // de la fase anterior: su primera columna cubre IdDetalle.

            // Registro financiero obligatorio y único para cada anulación.
            // Ningún borrado en cascada puede ocultar un reembolso pendiente.
            modelBuilder.Entity<Reembolso>(entidad =>
            {
                entidad.ToTable("Reembolsos", tabla =>
                {
                    tabla.HasCheckConstraint(
                        "CK_Reembolsos_Estado",
                        "[Estado] IN (N'Pendiente', N'PendienteRevision', N'Aprobado', N'Procesando', N'Reembolsado', N'Rechazado', N'Fallido')");
                    tabla.HasCheckConstraint(
                        "CK_Reembolsos_MontoSolicitado",
                        "[MontoSolicitado] >= 0");
                });
                entidad.HasIndex(r => new { r.IdEmpresa, r.IdFactura }).IsUnique();
                entidad.HasOne(r => r.Factura)
                    .WithMany()
                    .HasForeignKey(r => new { r.IdEmpresa, r.IdFactura })
                    .HasPrincipalKey(f => new { f.IdEmpresa, f.IdFactura })
                    .OnDelete(DeleteBehavior.NoAction);
            });

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
            ConfigurarEmpresaInicial<Reembolso>(modelBuilder);

            // Factura -> Detalles/Pagos conserva su configuración existente.
        }

        private void ConfigurarEmpresaInicial<T>(ModelBuilder modelBuilder)
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

            // EF incluye este predicado en consultas, proyecciones y en
            // ExecuteUpdate / ExecuteDelete (salvo IgnoreQueryFilters explícito).
            modelBuilder.Entity<T>()
                .HasQueryFilter(registro =>
                    EF.Property<Guid>(registro, "IdEmpresa") == IdEmpresaActual);
        }

        public override int SaveChanges()
        {
            ValidarEscriturasDeEmpresa();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ValidarEscriturasDeEmpresa();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            ValidarEscriturasDeEmpresa();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            ValidarEscriturasDeEmpresa();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void ValidarEscriturasDeEmpresa()
        {
            var entradas = ChangeTracker.Entries<IEntidadEmpresa>()
                .Where(e => e.State == EntityState.Added ||
                            e.State == EntityState.Modified ||
                            e.State == EntityState.Deleted)
                .ToList();

            if (entradas.Count == 0)
                return; // Identity y tablas globales tienen ciclo independiente.

            var idEmpresa = IdEmpresaActual;
            if (idEmpresa == Guid.Empty)
                throw new UnauthorizedAccessException(
                    "No hay una empresa validada para realizar operaciones comerciales.");

            foreach (var entrada in entradas)
            {
                var propiedad = entrada.Property(e => e.IdEmpresa);

                if (entrada.State == EntityState.Added)
                {
                    if (entrada.Entity.IdEmpresa != Guid.Empty &&
                        entrada.Entity.IdEmpresa != idEmpresa)
                        throw new UnauthorizedAccessException(
                            "No puedes crear datos en otra empresa.");

                    entrada.Entity.IdEmpresa = idEmpresa;
                }
                else
                {
                    if (entrada.Entity.IdEmpresa != idEmpresa ||
                        propiedad.OriginalValue != idEmpresa)
                        throw new UnauthorizedAccessException(
                            "No puedes modificar datos de otra empresa.");

                    // La pertenencia del registro no es editable.
                    if (entrada.State == EntityState.Modified)
                        propiedad.IsModified = false;
                }
            }
        }

    }
}
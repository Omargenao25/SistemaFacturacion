using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models;
using SistemaFacturacion.Services.Tenancy;
using Xunit;

namespace NexoraSecurity.Tests;

/// <summary>
/// Verifica el contrato EF Core del modelo; no sustituye pruebas reales de
/// restricciones SQL Server ni habilita la emision de notas de credito.
/// </summary>
public sealed class NotasCreditoModeloIntegridadTests
{
    private sealed class ContextoPrueba : IContextoEmpresa
    {
        public Guid IdEmpresa { get; } = Guid.Parse("11111111-1111-4111-8111-111111111111");
        public bool TieneEmpresa => true;
    }

    private static ApplicationDbContext Crear() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options,
            new ContextoPrueba());

    [Fact]
    public void NotaCredito_ReferenciaFacturaDeLaMismaEmpresa()
    {
        using var db = Crear();
        var entidad = db.Model.FindEntityType(typeof(NotaCredito))!;
        var fk = Assert.Single(entidad.GetForeignKeys().Where(x => x.PrincipalEntityType.ClrType == typeof(Factura)));
        Assert.Equal(new[] { "IdEmpresa", "IdFactura" }, fk.Properties.Select(p => p.Name));
        Assert.Equal(new[] { "IdEmpresa", "IdFactura" }, fk.PrincipalKey.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
    }

    [Fact]
    public void DetalleCredito_ReferenciaNotaYLineaOriginalDentroDeEmpresa()
    {
        using var db = Crear();
        var entidad = db.Model.FindEntityType(typeof(DetalleNotaCredito))!;
        var relaciones = entidad.GetForeignKeys().Where(x =>
            x.PrincipalEntityType.ClrType == typeof(NotaCredito) ||
            x.PrincipalEntityType.ClrType == typeof(DetalleFactura)).ToArray();
        Assert.Equal(2, relaciones.Length);

        var aNota = Assert.Single(relaciones.Where(x => x.PrincipalEntityType.ClrType == typeof(NotaCredito)));
        Assert.Equal(new[] { "IdEmpresa", "IdNotaCredito" }, aNota.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.NoAction, aNota.DeleteBehavior);

        var aVenta = Assert.Single(relaciones.Where(x => x.PrincipalEntityType.ClrType == typeof(DetalleFactura)));
        Assert.Equal(new[] { "IdEmpresa", "IdDetalleFactura" }, aVenta.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.NoAction, aVenta.DeleteBehavior);
    }

    [Fact]
    public void AmbosModelos_TienenFiltroDeEmpresaYVersionDeConcurrencia()
    {
        using var db = Crear();
        var nota = db.Model.FindEntityType(typeof(NotaCredito))!;
        var detalle = db.Model.FindEntityType(typeof(DetalleNotaCredito))!;

        Assert.NotNull(nota.GetQueryFilter());
        Assert.NotNull(detalle.GetQueryFilter());
        Assert.True(nota.FindProperty(nameof(NotaCredito.RowVersion))!.IsConcurrencyToken);
    }
}

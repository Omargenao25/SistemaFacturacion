using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models;
using SistemaFacturacion.Services.Tenancy;
using Xunit;

namespace NexoraSecurity.Tests;

public sealed class NotasCreditoTenantTests
{
    private static readonly Guid A = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid B = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private sealed class Tenant(Guid id) : IContextoEmpresa
    {
        public Guid IdEmpresa => id;
        public bool TieneEmpresa => id != Guid.Empty;
    }

    private static ApplicationDbContext Crear(string database, Guid empresa) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(database).Options, new Tenant(empresa));

    [Fact]
    public async Task B_NoPuedeConsultarBorradorDeA()
    {
        var db = Guid.NewGuid().ToString("N");
        await using (var a = Crear(db, A))
        {
            a.NotasCredito.Add(new NotaCredito { IdNotaCredito = 1, IdEmpresa = A, IdFactura = 1, Motivo = "Prueba", Estado = "Borrador" });
            await a.SaveChangesAsync();
        }
        await using var b = Crear(db, B);
        Assert.Empty(await b.NotasCredito.ToListAsync());
        Assert.Null(await b.NotasCredito.FindAsync(999));
    }

    [Fact]
    public async Task SinEmpresa_NoPuedeConsultarBorradores()
    {
        var db = Guid.NewGuid().ToString("N");
        await using (var a = Crear(db, A))
        {
            a.NotasCredito.Add(new NotaCredito { IdNotaCredito = 1, IdFactura = 1, Motivo = "Prueba" });
            await a.SaveChangesAsync();
        }
        await using var sinEmpresa = Crear(db, Guid.Empty);
        Assert.Empty(await sinEmpresa.NotasCredito.ToListAsync());
    }

    [Fact]
    public async Task B_NoPuedeCrearBorradorMarcadoComoA()
    {
        var db = Guid.NewGuid().ToString("N");
        await using var b = Crear(db, B);
        b.NotasCredito.Add(new NotaCredito { IdEmpresa = A, IdFactura = 1, Motivo = "Cruce" });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.SaveChangesAsync());
    }

    [Fact]
    public async Task B_NoPuedeCrearDetalleMarcadoComoA()
    {
        var db = Guid.NewGuid().ToString("N");
        await using var b = Crear(db, B);
        b.DetallesNotaCredito.Add(new DetalleNotaCredito
        {
            IdEmpresa = A, IdNotaCredito = 1, IdDetalleFactura = 1, Cantidad = 1
        });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.SaveChangesAsync());
    }

    [Fact]
    public void Modelo_ConfiguraFiltroParaAmbasEntidades()
    {
        using var a = Crear(Guid.NewGuid().ToString("N"), A);
        Assert.NotNull(a.Model.FindEntityType(typeof(NotaCredito))?.GetQueryFilter());
        Assert.NotNull(a.Model.FindEntityType(typeof(DetalleNotaCredito))?.GetQueryFilter());
    }
}

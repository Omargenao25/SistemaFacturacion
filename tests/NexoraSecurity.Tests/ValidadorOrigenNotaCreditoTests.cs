using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models;
using SistemaFacturacion.Services.Finanzas;
using SistemaFacturacion.Services.Tenancy;
using Xunit;

namespace NexoraSecurity.Tests;

public sealed class ValidadorOrigenNotaCreditoTests
{
    private static readonly Guid EmpresaA = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid EmpresaB = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private sealed class Tenant(Guid id) : IContextoEmpresa
    {
        public Guid IdEmpresa => id;
        public bool TieneEmpresa => id != Guid.Empty;
    }

    private static ApplicationDbContext Crear(string db, Guid empresa) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(db).Options,
            new Tenant(empresa));

    private static NotaCredito Borrador(int factura = 10, int linea = 20, Guid? empresa = null)
    {
        var idEmpresa = empresa ?? EmpresaA;
        var nota = new NotaCredito { IdEmpresa = idEmpresa, IdNotaCredito = 5, IdFactura = factura, Motivo = "Devolucion" };
        nota.Detalles.Add(new DetalleNotaCredito
        {
            IdEmpresa = idEmpresa, IdNotaCredito = 5, IdDetalleFactura = linea, Cantidad = 1
        });
        return nota;
    }

    private static async Task Preparar(string db)
    {
        await using (var a = Crear(db, EmpresaA))
        {
            a.Facturas.AddRange(
                new Factura { IdEmpresa = EmpresaA, IdFactura = 10, IdCliente = 1, NumeroFactura = "A-10" },
                new Factura { IdEmpresa = EmpresaA, IdFactura = 11, IdCliente = 1, NumeroFactura = "A-11" });
            a.DetallesFactura.AddRange(
                new DetalleFactura { IdEmpresa = EmpresaA, IdDetalle = 20, IdFactura = 10, IdProducto = 1, Cantidad = 3 },
                new DetalleFactura { IdEmpresa = EmpresaA, IdDetalle = 21, IdFactura = 11, IdProducto = 1, Cantidad = 3 });
            await a.SaveChangesAsync();
        }
        await using (var b = Crear(db, EmpresaB))
        {
            b.Facturas.Add(new Factura { IdEmpresa = EmpresaB, IdFactura = 12, IdCliente = 2, NumeroFactura = "B-12" });
            b.DetallesFactura.Add(new DetalleFactura { IdEmpresa = EmpresaB, IdDetalle = 22, IdFactura = 12, IdProducto = 2, Cantidad = 3 });
            await b.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task AceptaLineaDeFacturaDelTenant()
    {
        var nombre = Guid.NewGuid().ToString("N");
        await Preparar(nombre);
        await using var a = Crear(nombre, EmpresaA);
        await new ValidadorOrigenNotaCredito(a).ValidarAsync(Borrador());
    }

    [Fact]
    public async Task RechazaLineaDeOtraFacturaDeLaMismaEmpresa()
    {
        var nombre = Guid.NewGuid().ToString("N");
        await Preparar(nombre);
        await using var a = Crear(nombre, EmpresaA);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ValidadorOrigenNotaCredito(a).ValidarAsync(Borrador(linea: 21)));
    }

    [Fact]
    public async Task RechazaFacturaDeOtraEmpresa()
    {
        var nombre = Guid.NewGuid().ToString("N");
        await Preparar(nombre);
        await using var b = Crear(nombre, EmpresaB);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ValidadorOrigenNotaCredito(b).ValidarAsync(Borrador()));
    }

    [Fact]
    public async Task RechazaDetalleDeOtraEmpresa()
    {
        var nombre = Guid.NewGuid().ToString("N");
        await Preparar(nombre);
        await using var a = Crear(nombre, EmpresaA);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ValidadorOrigenNotaCredito(a).ValidarAsync(Borrador(linea: 22)));
    }

    [Fact]
    public async Task SinTenantVerificado_RechazaNotaDeA()
    {
        var nombre = Guid.NewGuid().ToString("N");
        await Preparar(nombre);
        await using var sinEmpresa = Crear(nombre, Guid.Empty);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ValidadorOrigenNotaCredito(sinEmpresa).ValidarAsync(Borrador()));
    }
}

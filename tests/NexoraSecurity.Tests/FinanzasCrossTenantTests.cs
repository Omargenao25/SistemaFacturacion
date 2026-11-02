using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models;
using SistemaFacturacion.Models.SaaS;
using SistemaFacturacion.Services.Tenancy;
using Xunit;

namespace NexoraSecurity.Tests;

public sealed class FinanzasCrossTenantTests
{
    private static readonly Guid A = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid B = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private sealed class Tenant(Guid id) : IContextoEmpresa
    {
        public Guid IdEmpresa => id;
        public bool TieneEmpresa => id != Guid.Empty;
    }

    private static ApplicationDbContext Crear(string nombre, Guid empresa) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nombre).Options, new Tenant(empresa));

    private static async Task Preparar(string nombre)
    {
        await using var a = Crear(nombre, A);
        a.Empresas.AddRange(
            new Empresa { IdEmpresa = A, SlugNormalizado = "nexora-lab-a", NombreComercial = "A", Estado = "Activa" },
            new Empresa { IdEmpresa = B, SlugNormalizado = "nexora-lab-b", NombreComercial = "B", Estado = "Activa" });
        a.Clientes.Add(new Cliente { IdEmpresa = A, IdCliente = 1, Nombre = "Cliente", Apellido = "A" });
        a.Facturas.Add(new Factura
        {
            IdEmpresa = A, IdFactura = 1, IdCliente = 1, NumeroFactura = "LAB-A-0001",
            Total = 118m, Subtotal = 100m, Impuesto = 18m, Estado = "Anulada"
        });
        a.Reembolsos.Add(new Reembolso
        {
            IdEmpresa = A, IdReembolso = 1, IdFactura = 1, MontoSolicitado = 118m,
            MetodoPagoOriginal = "Efectivo", Estado = "Pendiente"
        });
        await a.SaveChangesAsync();
    }

    [Fact]
    public async Task EmpresaB_NoPuedeLeerSolicitudDeReembolsoDeA()
    {
        var nombre = Guid.NewGuid().ToString("N");
        await Preparar(nombre);
        await using var b = Crear(nombre, B);
        Assert.Empty(await b.Reembolsos.ToListAsync());
        Assert.Null(await b.Reembolsos.SingleOrDefaultAsync(r => r.IdReembolso == 1));
        Assert.Empty(await b.Facturas.ToListAsync());
    }

    [Fact]
    public async Task SinEmpresaVerificada_NoPuedeLeerReembolsos()
    {
        var nombre = Guid.NewGuid().ToString("N");
        await Preparar(nombre);
        await using var sinEmpresa = Crear(nombre, Guid.Empty);
        Assert.Empty(await sinEmpresa.Reembolsos.ToListAsync());
    }

    [Fact]
    public async Task EmpresaB_NoPuedeRegistrarReembolsoAsignadoAA()
    {
        var nombre = Guid.NewGuid().ToString("N");
        await using var b = Crear(nombre, B);
        b.Reembolsos.Add(new Reembolso
        {
            IdEmpresa = A, IdFactura = 1, MontoSolicitado = 100m,
            MetodoPagoOriginal = "Efectivo", Estado = "Pendiente"
        });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.SaveChangesAsync());
    }

    [Fact]
    public async Task SolicitudPendiente_NoEsComprobanteDeDineroDevuelto()
    {
        var nombre = Guid.NewGuid().ToString("N");
        await Preparar(nombre);
        await using var a = Crear(nombre, A);
        var solicitud = await a.Reembolsos.SingleAsync();
        Assert.Equal("Pendiente", solicitud.Estado);
        Assert.Null(solicitud.FechaConfirmacionUtc);
        Assert.Null(solicitud.ConfirmadoPorUserId);
        Assert.Null(solicitud.ReferenciaDevolucion);
    }
}

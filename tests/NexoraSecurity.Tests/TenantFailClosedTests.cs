using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models;
using SistemaFacturacion.Models.SaaS;
using SistemaFacturacion.Services.Tenancy;
using Xunit;

namespace NexoraSecurity.Tests;

public sealed class TenantFailClosedTests
{
    private static readonly Guid EmpresaA = Guid.Parse("11111111-1111-4111-8111-111111111111");

    private sealed class Tenant(Guid id) : IContextoEmpresa
    {
        public Guid IdEmpresa => id;
        public bool TieneEmpresa => id != Guid.Empty;
    }

    private static ApplicationDbContext Contexto(string baseNombre, Guid empresa) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(baseNombre).Options, new Tenant(empresa));

    [Fact]
    public async Task SinEmpresaVerificada_NoPuedeLeerRegistrosComerciales()
    {
        var baseNombre = Guid.NewGuid().ToString("N");
        await using (var creador = Contexto(baseNombre, EmpresaA))
        {
            creador.Empresas.Add(new Empresa
            {
                IdEmpresa = EmpresaA,
                NombreComercial = "Empresa A",
                SlugNormalizado = "nexora-lab-a",
                Estado = "Activa"
            });
            creador.Clientes.Add(new Cliente
            {
                IdCliente = 1,
                IdEmpresa = EmpresaA,
                Nombre = "Cliente",
                Apellido = "A"
            });
            await creador.SaveChangesAsync();
        }

        await using var sinEmpresa = Contexto(baseNombre, Guid.Empty);
        Assert.Empty(await sinEmpresa.Clientes.ToListAsync());
    }

    [Fact]
    public async Task SinEmpresaVerificada_NoPuedeCrearRegistrosComerciales()
    {
        var baseNombre = Guid.NewGuid().ToString("N");
        await using var sinEmpresa = Contexto(baseNombre, Guid.Empty);
        sinEmpresa.Clientes.Add(new Cliente { Nombre = "Intruso", Apellido = "Sin tenant" });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sinEmpresa.SaveChangesAsync());
    }

    [Fact]
    public async Task EmpresaB_NoPuedeCrearRegistroConIdEmpresaDeA()
    {
        var baseNombre = Guid.NewGuid().ToString("N");
        var empresaB = Guid.Parse("22222222-2222-4222-8222-222222222222");
        await using var contexto = Contexto(baseNombre, empresaB);
        contexto.Clientes.Add(new Cliente
        {
            IdEmpresa = EmpresaA,
            Nombre = "Intruso",
            Apellido = "Cruce"
        });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => contexto.SaveChangesAsync());
    }
}

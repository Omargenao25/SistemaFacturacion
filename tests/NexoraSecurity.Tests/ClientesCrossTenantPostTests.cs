using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Controllers;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models;
using SistemaFacturacion.Models.SaaS;
using SistemaFacturacion.Services.Tenancy;
using Xunit;

namespace NexoraSecurity.Tests;

public sealed class ClientesCrossTenantPostTests
{
    private static readonly Guid EmpresaA = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid EmpresaB = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private sealed class Tenant(Guid id) : IContextoEmpresa
    {
        public Guid IdEmpresa => id;
        public bool TieneEmpresa => id != Guid.Empty;
    }

    private static ApplicationDbContext Contexto(string db, Guid empresa) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(db).Options,
            new Tenant(empresa));

    private static async Task PrepararAsync(string db)
    {
        await using var contexto = Contexto(db, EmpresaA);
        contexto.Empresas.AddRange(
            new Empresa { IdEmpresa = EmpresaA, SlugNormalizado = "nexora-lab-a", NombreComercial = "A", Estado = "Activa" },
            new Empresa { IdEmpresa = EmpresaB, SlugNormalizado = "nexora-lab-b", NombreComercial = "B", Estado = "Activa" });
        contexto.Clientes.Add(new Cliente
        {
            IdEmpresa = EmpresaA,
            IdCliente = 1,
            Nombre = "Cliente",
            Apellido = "Prueba A"
        });
        await contexto.SaveChangesAsync();
    }

    [Fact]
    public async Task EditPost_DeEmpresaB_NoModificaClienteDeA()
    {
        var db = Guid.NewGuid().ToString("N");
        await PrepararAsync(db);
        await using (var contextoB = Contexto(db, EmpresaB))
        {
            var controller = new ClientesController(contextoB);
            var intento = new Cliente
            {
                IdCliente = 1,
                Nombre = "Manipulado",
                Apellido = "Desde B"
            };
            Assert.IsType<NotFoundResult>(await controller.Edit(1, intento));
        }

        await using var comprobacion = Contexto(db, EmpresaA);
        var original = await comprobacion.Clientes.SingleAsync(c => c.IdCliente == 1);
        Assert.Equal("Cliente", original.Nombre);
        Assert.Equal("Prueba A", original.Apellido);
    }

    [Fact]
    public async Task DeletePost_DeEmpresaB_NoEliminaClienteDeA()
    {
        var db = Guid.NewGuid().ToString("N");
        await PrepararAsync(db);
        await using (var contextoB = Contexto(db, EmpresaB))
        {
            var controller = new ClientesController(contextoB);
            Assert.IsType<NotFoundResult>(await controller.DeleteConfirmed(1));
        }

        await using var comprobacion = Contexto(db, EmpresaA);
        Assert.Equal(1, await comprobacion.Clientes.CountAsync(c => c.IdCliente == 1));
    }

    [Fact]
    public async Task Consulta_DeEmpresaB_NoExponeClienteDeA()
    {
        var db = Guid.NewGuid().ToString("N");
        await PrepararAsync(db);
        await using var contextoB = Contexto(db, EmpresaB);
        var controller = new ClientesController(contextoB);
        Assert.IsType<NotFoundResult>(await controller.Details(1));
        Assert.Empty(await contextoB.Clientes.ToListAsync());
    }
}

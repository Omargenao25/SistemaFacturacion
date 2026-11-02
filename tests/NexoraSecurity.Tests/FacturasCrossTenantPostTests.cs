using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Controllers;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models;
using SistemaFacturacion.Models.SaaS;
using SistemaFacturacion.Services.Tenancy;
using SistemaFacturacion.ViewModels;
using Xunit;

namespace NexoraSecurity.Tests;

public sealed class FacturasCrossTenantPostTests
{
    private static readonly Guid A = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid B = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private sealed class Tenant(Guid empresa) : IContextoEmpresa
    {
        public Guid IdEmpresa => empresa;
        public bool TieneEmpresa => empresa != Guid.Empty;
    }

    private static ApplicationDbContext Contexto(string nombre, Guid empresa) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nombre).Options, new Tenant(empresa));

    private static async Task PrepararAsync(string nombre)
    {
        await using (var a = Contexto(nombre, A))
        {
            a.Empresas.AddRange(
                new Empresa { IdEmpresa = A, SlugNormalizado = "nexora-lab-a", NombreComercial = "A", Estado = "Activa" },
                new Empresa { IdEmpresa = B, SlugNormalizado = "nexora-lab-b", NombreComercial = "B", Estado = "Activa" });
            a.Categorias.Add(new Categoria { IdEmpresa = A, IdCategoria = 1, Nombre = "Categoria A" });
            a.Clientes.Add(new Cliente { IdEmpresa = A, IdCliente = 1, Nombre = "Cliente", Apellido = "A" });
            a.Productos.Add(new Producto { IdEmpresa = A, IdProducto = 1, IdCategoria = 1, Codigo = "LAB-A-001", Nombre = "Laptop A", Precio = 25000, Stock = 10, RowVersion = new byte[8] });
            await a.SaveChangesAsync();
        }
        await using (var b = Contexto(nombre, B))
        {
            b.Categorias.Add(new Categoria { IdEmpresa = B, IdCategoria = 2, Nombre = "Categoria B" });
            b.Clientes.Add(new Cliente { IdEmpresa = B, IdCliente = 2, Nombre = "Cliente", Apellido = "B" });
            b.Productos.Add(new Producto { IdEmpresa = B, IdProducto = 2, IdCategoria = 2, Codigo = "LAB-B-001", Nombre = "Monitor B", Precio = 12000, Stock = 13, RowVersion = new byte[8] });
            await b.SaveChangesAsync();
        }
    }

    [Theory]
    [InlineData(1, 2)] // Cliente de A con producto válido de B.
    [InlineData(2, 1)] // Producto de A con cliente válido de B.
    public async Task CrearFactura_ReferenciaAjena_NoCreaFacturaNiCambiaStock(int clienteId, int productoId)
    {
        var nombre = Guid.NewGuid().ToString("N");
        await PrepararAsync(nombre);

        await using (var contextoB = Contexto(nombre, B))
        {
            var controller = new FacturasController(contextoB);
            var modelo = new CrearFacturaViewModel
            {
                IdCliente = clienteId,
                MetodoPago = "Efectivo",
                Detalles = new List<DetalleFacturaInput> { new() { IdProducto = productoId, Cantidad = 1 } }
            };
            var respuesta = await controller.Create(modelo);
            Assert.IsType<ViewResult>(respuesta);
            Assert.False(controller.ModelState.IsValid);
        }

        await using var verificarA = Contexto(nombre, A);
        await using var verificarB = Contexto(nombre, B);
        Assert.Equal(10, (await verificarA.Productos.SingleAsync(p => p.IdProducto == 1)).Stock);
        Assert.Equal(13, (await verificarB.Productos.SingleAsync(p => p.IdProducto == 2)).Stock);
        Assert.Empty(await verificarA.Facturas.ToListAsync());
        Assert.Empty(await verificarB.Facturas.ToListAsync());
    }
}

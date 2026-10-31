using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Controllers;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models;
using SistemaFacturacion.Models.SaaS;
using SistemaFacturacion.Services.Tenancy;
using Xunit;

namespace NexoraSecurity.Tests;

public sealed class ProductosCrossTenantPostTests
{
    private static readonly Guid EmpresaA = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid EmpresaB = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private sealed class Tenant(Guid id) : IContextoEmpresa
    {
        public Guid IdEmpresa => id;
        public bool TieneEmpresa => id != Guid.Empty;
    }

    private static ApplicationDbContext CrearContexto(string baseNombre, Guid empresa)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(baseNombre)
            .Options;
        return new ApplicationDbContext(options, new Tenant(empresa));
    }

    private static async Task PrepararDatos(string nombre)
    {
        await using var db = CrearContexto(nombre, EmpresaA);
        db.Empresas.AddRange(
            new Empresa { IdEmpresa = EmpresaA, SlugNormalizado = "nexora-lab-a", NombreComercial = "Lab A", Estado = "Activa" },
            new Empresa { IdEmpresa = EmpresaB, SlugNormalizado = "nexora-lab-b", NombreComercial = "Lab B", Estado = "Activa" });
        db.Categorias.Add(new Categoria { IdCategoria = 1, IdEmpresa = EmpresaA, Nombre = "Categoria A" });
        db.Productos.Add(new Producto
        {
            IdProducto = 1,
            IdEmpresa = EmpresaA,
            IdCategoria = 1,
            Nombre = "Laptop Prueba A",
            Codigo = "LAB-A-001",
            Stock = 10,
            Precio = 25000m,
            RowVersion = new byte[8]
        });
        await db.SaveChangesAsync();
        // Seed category of B as a separate authorized tenant to ensure an
        // attempted Edit POST passes its category-membership precheck.
        await using var dbB = CrearContexto(nombre, EmpresaB);
        dbB.Categorias.Add(new Categoria { IdCategoria = 2, IdEmpresa = EmpresaB, Nombre = "Categoria B" });
        await dbB.SaveChangesAsync();
    }

    [Fact]
    public async Task DeletePost_DeEmpresaB_NoPuedeEliminarProductoDeA()
    {
        var nombre = Guid.NewGuid().ToString("N");
        await PrepararDatos(nombre);
        await using (var dbB = CrearContexto(nombre, EmpresaB))
        {
            var controller = new ProductosController(dbB);
            var respuesta = await controller.DeleteConfirmed(1);
            Assert.IsType<NotFoundResult>(respuesta);
        }
        await using var verificacionA = CrearContexto(nombre, EmpresaA);
        var producto = await verificacionA.Productos.SingleAsync(p => p.IdProducto == 1);
        Assert.Equal(10, producto.Stock);
        Assert.Equal("Laptop Prueba A", producto.Nombre);
    }

    [Fact]
    public async Task EditPost_DeEmpresaB_NoPuedeModificarProductoDeA()
    {
        var nombre = Guid.NewGuid().ToString("N");
        await PrepararDatos(nombre);
        await using (var dbB = CrearContexto(nombre, EmpresaB))
        {
            var controller = new ProductosController(dbB);
            var intento = new Producto
            {
                IdProducto = 1,
                IdCategoria = 2,
                Codigo = "CAMBIADO",
                Nombre = "Acceso ajeno",
                Stock = 0,
                Precio = 1m,
                RowVersion = new byte[8]
            };
            var respuesta = await controller.Edit(1, intento);
            Assert.IsType<NotFoundResult>(respuesta);
        }
        await using var verificacionA = CrearContexto(nombre, EmpresaA);
        var producto = await verificacionA.Productos.SingleAsync(p => p.IdProducto == 1);
        Assert.Equal("LAB-A-001", producto.Codigo);
        Assert.Equal(10, producto.Stock);
        Assert.Equal("Laptop Prueba A", producto.Nombre);
    }
}

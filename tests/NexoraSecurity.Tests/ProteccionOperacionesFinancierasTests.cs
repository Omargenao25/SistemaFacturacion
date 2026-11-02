using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using SistemaFacturacion.Controllers;
using SistemaFacturacion.Security;
using Xunit;

namespace NexoraSecurity.Tests;

/// <summary>
/// Contrato de seguridad de las acciones que pueden cambiar facturas,
/// clientes, productos o permisos. No toca SQL Server ni mueve dinero.
/// </summary>
public sealed class ProteccionOperacionesFinancierasTests
{
    [Theory]
    [InlineData(typeof(FacturasController), "Anular", PermisosNexora.AnularFacturas)]
    [InlineData(typeof(FacturasController), "Create", PermisosNexora.CrearFacturas)]
    [InlineData(typeof(ProductosController), "DeleteConfirmed", PermisosNexora.AdministrarInventario)]
    [InlineData(typeof(ClientesController), "DeleteConfirmed", PermisosNexora.AdministrarClientes)]
    public void PostSensible_RequiereAntiforgeryYPermiso(
        Type controlador, string accion, string permisoEsperado)
    {
        var metodo = controlador.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(m => m.Name == accion &&
                         m.GetCustomAttributes<HttpPostAttribute>().Any());

        Assert.NotNull(metodo.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        var nombreAccion = metodo.GetCustomAttribute<ActionNameAttribute>()?.Name
            ?? metodo.Name;
        Assert.Equal(permisoEsperado,
            ReglasAccesoControladores.ObtenerPermiso(
                controlador.Name.Replace("Controller", ""), nombreAccion, "POST"));
        if (nombreAccion == "Anular")
            Assert.Null(ReglasAccesoControladores.ObtenerPermiso(
                "Facturas", "Anular", "GET"));
    }

    [Fact]
    public void AnularFactura_NoAutorizaCajero()
    {
        Assert.False(PermisosNexora.TienePermiso("Cajero", PermisosNexora.AnularFacturas));
        Assert.True(PermisosNexora.TienePermiso("Administrador", PermisosNexora.AnularFacturas));
        Assert.True(PermisosNexora.TienePermiso("Propietario", PermisosNexora.AnularFacturas));
    }

    [Fact]
    public void ReembolsoYRecepcion_NoTienenPostHabilitadoEnMapaComercial()
    {
        Assert.Null(ReglasAccesoControladores.ObtenerPermiso("ControlAnulaciones", "ConfirmarReembolso", "POST"));
        Assert.Null(ReglasAccesoControladores.ObtenerPermiso("Recepciones", "LiberarInventario", "POST"));
        Assert.Null(ReglasAccesoControladores.ObtenerPermiso("ControlAnulaciones", "Index", "POST"));
    }
}

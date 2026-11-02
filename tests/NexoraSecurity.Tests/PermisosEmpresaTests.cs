using SistemaFacturacion.Security;
using Xunit;

namespace NexoraSecurity.Tests;

public sealed class PermisosEmpresaTests
{
    [Theory]
    [InlineData("Cajero", PermisosNexora.ConsultarOperaciones, true)]
    [InlineData("Cajero", PermisosNexora.CrearFacturas, true)]
    [InlineData("Cajero", PermisosNexora.AdministrarInventario, false)]
    [InlineData("Cajero", PermisosNexora.AdministrarClientes, false)]
    [InlineData("Cajero", PermisosNexora.ConsultarReportes, false)]
    [InlineData("Cajero", PermisosNexora.AdministrarUsuarios, false)]
    [InlineData("Administrador", PermisosNexora.AdministrarInventario, true)]
    [InlineData("Administrador", PermisosNexora.ConsultarReportes, true)]
    [InlineData("Administrador", PermisosNexora.AdministrarUsuarios, false)]
    [InlineData("Propietario", PermisosNexora.AdministrarUsuarios, true)]
    [InlineData("Propietario", PermisosNexora.AdministrarRoles, true)]
    [InlineData("RolNoReconocido", PermisosNexora.ConsultarOperaciones, false)]
    public void MatrizRoles_RespetaPermisos(string rol, string permiso, bool esperado) =>
        Assert.Equal(esperado, PermisosNexora.TienePermiso(rol, permiso));

    [Theory]
    [InlineData("Productos", "Edit", "POST", PermisosNexora.AdministrarInventario)]
    [InlineData("Productos", "Delete", "POST", PermisosNexora.AdministrarInventario)]
    [InlineData("Clientes", "Edit", "POST", PermisosNexora.AdministrarClientes)]
    [InlineData("Clientes", "Delete", "POST", PermisosNexora.AdministrarClientes)]
    [InlineData("Facturas", "Create", "POST", PermisosNexora.CrearFacturas)]
    [InlineData("Equipo", "CambiarRol", "POST", PermisosNexora.AdministrarRoles)]
    public void PostComercial_ExigePermisoEspecifico(string controlador, string accion, string metodo, string permiso) =>
        Assert.Equal(permiso, ReglasAccesoControladores.ObtenerPermiso(controlador, accion, metodo));

    [Theory]
    [InlineData("Productos", "Edit", "PUT")]
    [InlineData("Clientes", "Delete", "DELETE")]
    [InlineData("Facturas", "AccionInventada", "POST")]
    [InlineData("Equipo", "CambiarRol", "GET")]
    public void OperacionNoRegistrada_SeDeniegaPorDefecto(string controlador, string accion, string metodo) =>
        Assert.Null(ReglasAccesoControladores.ObtenerPermiso(controlador, accion, metodo));
}

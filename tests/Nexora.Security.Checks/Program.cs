using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SistemaFacturacion.Security;
using System.Net.Mail;
using SistemaFacturacion.Services.Tenancy;

var fallos = 0;
var pruebas = new (string Nombre, Func<Task> Ejecutar)[]
{
    ("Cajero no confirma reembolsos", () =>
    {
        Verificar(!PermisosNexora.TienePermiso("Cajero", PermisosNexora.ConfirmarReembolsos));
        return Task.CompletedTask;
    }),
    ("Administrador no se asigna roles", () =>
    {
        Verificar(!PermisosNexora.TienePermiso("Administrador", PermisosNexora.AdministrarRoles));
        return Task.CompletedTask;
    }),
    ("Cajero sí puede crear factura", () =>
    {
        Verificar(PermisosNexora.TienePermiso("Cajero", PermisosNexora.CrearFacturas));
        return Task.CompletedTask;
    }),
    ("Rol desconocido falla cerrado", () =>
    {
        Verificar(!PermisosNexora.TienePermiso("Superusuario", PermisosNexora.ConsultarOperaciones));
        Verificar(!PermisosNexora.TienePermiso(null, PermisosNexora.ConsultarOperaciones));
        Verificar(!PermisosNexora.TienePermiso("Propietario", "Nexora.Inventado"));
        return Task.CompletedTask;
    }),
    ("Petición sin slug no concede empresa", async () =>
    {
        var validador = new ValidadorFalso(
            new MembresiaEmpresaValidada(Guid.NewGuid(), "Propietario"));
        var contexto = new ContextoEmpresaVerificado();
        await Middleware().InvokeAsync(HttpAutenticado(), contexto, validador);
        Verificar(!contexto.TieneEmpresa && validador.Llamadas == 0);
    }),
    ("Anónimo no valida membresía", async () =>
    {
        var validador = new ValidadorFalso(
            new MembresiaEmpresaValidada(Guid.NewGuid(), "Propietario"));
        var contexto = new ContextoEmpresaVerificado();
        await Middleware().InvokeAsync(HttpAnonimo("tienda-a"), contexto, validador);
        Verificar(!contexto.TieneEmpresa && validador.Llamadas == 0);
    }),
    ("Membresía rechazada no autoriza", async () =>
    {
        var validador = new ValidadorFalso(null);
        var contexto = new ContextoEmpresaVerificado();
        await Middleware().InvokeAsync(HttpAutenticado("tienda-b"), contexto, validador);
        Verificar(!contexto.TieneEmpresa && validador.Llamadas == 1);
    }),
    ("Membresía válida vincula rol a esta petición", async () =>
    {
        var empresa = Guid.NewGuid();
        var validador = new ValidadorFalso(new MembresiaEmpresaValidada(empresa, "Cajero"));
        var contexto = new ContextoEmpresaVerificado();
        await Middleware().InvokeAsync(HttpAutenticado("tienda-a"), contexto, validador);
        Verificar(contexto.TieneEmpresa &&
                 contexto.IdEmpresa == empresa && contexto.RolEmpresa == "Cajero");
    }),
    ("Handler bloquea operación financiera al cajero", async () =>
    {
        var contexto = await CrearContextoValidado("Cajero");
        var requisito = new PermisoEmpresaRequirement(PermisosNexora.ConfirmarReembolsos);
        var evaluacion = new AuthorizationHandlerContext(
            new[] { requisito }, HttpAutenticado().User, resource: null);
        await new PermisoEmpresaHandler(contexto).HandleAsync(evaluacion);
        Verificar(!evaluacion.HasSucceeded);
    }),
    ("Handler permite consulta de diagnóstico a miembro", async () =>
    {
        var contexto = await CrearContextoValidado("Cajero");
        var requisito = new PermisoEmpresaRequirement(PermisosNexora.ConsultarOperaciones);
        var evaluacion = new AuthorizationHandlerContext(
            new[] { requisito }, HttpAutenticado().User, resource: null);
        await new PermisoEmpresaHandler(contexto).HandleAsync(evaluacion);
        Verificar(evaluacion.HasSucceeded);
    }),
    ("Contexto no se cambia dos veces durante la petición", async () =>
    {
        var contexto = await CrearContextoValidado("Administrador");
        var validador = new ValidadorFalso(
            new MembresiaEmpresaValidada(Guid.NewGuid(), "Propietario"));
        try
        {
            await Middleware().InvokeAsync(HttpAutenticado("otra-tienda"), contexto, validador);
            throw new Exception("Se permitió cambiar una membresía ya establecida.");
        }
        catch (InvalidOperationException)
        {
            Verificar(contexto.RolEmpresa == "Administrador");
        }
    }),
    ("Membresía con Guid.Empty se rechaza", async () =>
    {
        var contexto = new ContextoEmpresaVerificado();
        var validador = new ValidadorFalso(
            new MembresiaEmpresaValidada(Guid.Empty, "Propietario"));
        try
        {
            await Middleware().InvokeAsync(HttpAutenticado("tienda-a"), contexto, validador);
            throw new Exception("Se permitió un IdEmpresa vacío.");
        }
        catch (InvalidOperationException)
        {
            Verificar(!contexto.TieneEmpresa);
        }
    }),
    ("Cajero puede consultar, pero no anular facturas", () =>
    {
        var lectura = ReglasAccesoControladores.ObtenerPermiso("Facturas", "Details", "GET");
        var anular = ReglasAccesoControladores.ObtenerPermiso("Facturas", "Anular", "POST");
        Verificar(lectura == PermisosNexora.ConsultarOperaciones);
        Verificar(anular == PermisosNexora.AnularFacturas);
        Verificar(PermisosNexora.TienePermiso("Cajero", lectura!));
        Verificar(!PermisosNexora.TienePermiso("Cajero", anular!));
        return Task.CompletedTask;
    }),
    ("No se puede anular mediante GET", () =>
    {
        Verificar(ReglasAccesoControladores.ObtenerPermiso(
            "Facturas", "Anular", "GET") == null);
        return Task.CompletedTask;
    }),
    ("Métodos HTTP no previstos se deniegan", () =>
    {
        Verificar(ReglasAccesoControladores.ObtenerPermiso(
            "Facturas", "Create", "PUT") == null);
        Verificar(ReglasAccesoControladores.ObtenerPermiso(
            "Productos", "Edit", "PATCH") == null);
        Verificar(ReglasAccesoControladores.ObtenerPermiso(
            "Recepciones", "Details", "DELETE") == null);
        return Task.CompletedTask;
    }),
    ("Acciones nuevas y controladores desconocidos se deniegan", () =>
    {
        Verificar(ReglasAccesoControladores.ObtenerPermiso(
            "Facturas", "Reembolsar", "POST") == null);
        Verificar(ReglasAccesoControladores.ObtenerPermiso(
            "CualquierController", "Index", "GET") == null);
        Verificar(ReglasAccesoControladores.ObtenerPermiso(null, "Index", "GET") == null);
        return Task.CompletedTask;
    }),
    ("Editar stock exige administrar inventario", () =>
    {
        var permiso = ReglasAccesoControladores.ObtenerPermiso(
            "Productos", "Edit", "POST");
        Verificar(permiso == PermisosNexora.AdministrarInventario);
        Verificar(!PermisosNexora.TienePermiso("Cajero", permiso!));
        Verificar(PermisosNexora.TienePermiso("Administrador", permiso!));
        return Task.CompletedTask;
    }),
    ("Modificar clientes no se permite al cajero", () =>
    {
        var permiso = ReglasAccesoControladores.ObtenerPermiso(
            "Clientes", "Edit", "POST");
        Verificar(permiso == PermisosNexora.AdministrarClientes);
        Verificar(!PermisosNexora.TienePermiso("Cajero", permiso!));
        return Task.CompletedTask;
    }),
    ("Eliminar usa permiso aun con ActionName Delete", () =>
    {
        Verificar(ReglasAccesoControladores.ObtenerPermiso(
            "Categorias", "Delete", "POST") == PermisosNexora.AdministrarInventario);
        return Task.CompletedTask;
    }),
    ("Posventa: registrar no permite cerrar casos", () =>
    {
        var registrar = ReglasAccesoControladores.ObtenerPermiso(
            "Posventa", "Create", "POST");
        var cerrar = ReglasAccesoControladores.ObtenerPermiso(
            "Posventa", "CambiarEstado", "POST");
        Verificar(registrar == PermisosNexora.RegistrarPosventa);
        Verificar(cerrar == PermisosNexora.ResolverPosventa);
        Verificar(PermisosNexora.TienePermiso("Cajero", registrar!));
        Verificar(!PermisosNexora.TienePermiso("Cajero", cerrar!));
        return Task.CompletedTask;
    }),
    ("Recepciones siguen solo lectura", () =>
    {
        var consultar = ReglasAccesoControladores.ObtenerPermiso(
            "Recepciones", "Details", "GET");
        Verificar(consultar == PermisosNexora.ConsultarRecepciones);
        Verificar(!PermisosNexora.TienePermiso("Cajero", consultar!));
        Verificar(ReglasAccesoControladores.ObtenerPermiso(
            "Recepciones", "Confirmar", "POST") == null);
        return Task.CompletedTask;
    }),
    ("Centro de anulaciones requiere consulta financiera", () =>
    {
        var permiso = ReglasAccesoControladores.ObtenerPermiso(
            "ControlAnulaciones", "Index", "GET");
        Verificar(permiso == PermisosNexora.ConsultarReembolsos);
        Verificar(!PermisosNexora.TienePermiso("Cajero", permiso!));
        return Task.CompletedTask;
    }),
    ("Reportes requieren un permiso dedicado", () =>
    {
        var permiso = ReglasAccesoControladores.ObtenerPermiso(
            "Reportes", "Index", "GET");
        Verificar(permiso == PermisosNexora.ConsultarReportes);
        Verificar(!PermisosNexora.TienePermiso("Cajero", permiso!));
        return Task.CompletedTask;
    }),
    ("El directorio exige el permiso de administración de usuarios", () =>
    {
        var permiso = ReglasAccesoControladores.ObtenerPermiso(
            "Equipo", "Index", "GET");
        Verificar(permiso == PermisosNexora.AdministrarUsuarios);
        return Task.CompletedTask;
    }),
    ("El propietario puede consultar su directorio", async () =>
    {
        var contexto = await CrearContextoValidado("Propietario");
        var requisito = new PermisoEmpresaRequirement(PermisosNexora.AdministrarUsuarios);
        var autorizacion = new AuthorizationHandlerContext(
            new[] { requisito }, HttpAutenticado().User, resource: null);
        await new PermisoEmpresaHandler(contexto).HandleAsync(autorizacion);
        Verificar(autorizacion.HasSucceeded);
    }),
    ("Administrador y cajero no pueden consultar empleados", async () =>
    {
        foreach (var rol in new[] { "Administrador", "Cajero" })
        {
            var contexto = await CrearContextoValidado(rol);
            var requisito = new PermisoEmpresaRequirement(PermisosNexora.AdministrarUsuarios);
            var autorizacion = new AuthorizationHandlerContext(
                new[] { requisito }, HttpAutenticado().User, resource: null);
            await new PermisoEmpresaHandler(contexto).HandleAsync(autorizacion);
            Verificar(!autorizacion.HasSucceeded);
        }
    }),
    ("Sin empresa validada ningún rol accede al equipo", async () =>
    {
        var contexto = new ContextoEmpresaVerificado();
        var requisito = new PermisoEmpresaRequirement(PermisosNexora.AdministrarUsuarios);
        var autorizacion = new AuthorizationHandlerContext(
            new[] { requisito }, HttpAutenticado().User, resource: null);
        await new PermisoEmpresaHandler(contexto).HandleAsync(autorizacion);
        Verificar(!autorizacion.HasSucceeded);
    }),
    ("Los cambios de equipo solo usan POST con permiso explícito", () =>
    {
        Verificar(ReglasAccesoControladores.ObtenerPermiso(
            "Equipo", "CambiarRol", "POST") == PermisosNexora.AdministrarRoles);
        Verificar(ReglasAccesoControladores.ObtenerPermiso(
            "Equipo", "CambiarEstado", "POST") == PermisosNexora.AdministrarUsuarios);
        Verificar(ReglasAccesoControladores.ObtenerPermiso(
            "Equipo", "CrearInvitacion", "POST") == PermisosNexora.AdministrarUsuarios);
        return Task.CompletedTask;
    }),
    ("Las escrituras de equipo por GET se deniegan", () =>
    {
        foreach (var accion in new[] { "CambiarRol", "CambiarEstado", "CrearInvitacion" })
            Verificar(ReglasAccesoControladores.ObtenerPermiso(
                "Equipo", accion, "GET") == null);
        return Task.CompletedTask;
    }),
    ("Las escrituras no reconocidas continúan denegadas", () =>
    {
        foreach (var accion in new[] { "Create", "Edit", "Delete", "InvitarPropietario",
                                       "Escalar", "Transferir", "EliminarPropietario" })
            Verificar(ReglasAccesoControladores.ObtenerPermiso(
                "Equipo", accion, "POST") == null);
        return Task.CompletedTask;
    }),
    ("Un cajero no administra roles", () =>
    {
        Verificar(!PermisosNexora.TienePermiso("Cajero", PermisosNexora.AdministrarRoles));
        Verificar(!PermisosNexora.TienePermiso("Cajero", PermisosNexora.AdministrarUsuarios));
        return Task.CompletedTask;
    }),
    ("Solo propietario administra membresías", () =>
    {
        Verificar(PermisosNexora.TienePermiso("Propietario", PermisosNexora.AdministrarUsuarios));
        Verificar(PermisosNexora.TienePermiso("Propietario", PermisosNexora.AdministrarRoles));
        Verificar(!PermisosNexora.TienePermiso("Administrador", PermisosNexora.AdministrarUsuarios));
        return Task.CompletedTask;
    }),
    ("El rol Propietario no se puede delegar por invitación", () =>
    {
        Verificar(!GestionMembresiasSegura.RolDelegable("Propietario"));
        Verificar(GestionMembresiasSegura.RolDelegable("Cajero"));
        Verificar(GestionMembresiasSegura.RolDelegable("Administrador"));
        return Task.CompletedTask;
    }),
    ("Roles arbitrarios de invitación se rechazan", () =>
    {
        Verificar(!GestionMembresiasSegura.RolDelegable("Superusuario"));
        Verificar(!GestionMembresiasSegura.RolDelegable(null));
        return Task.CompletedTask;
    }),
    ("Correo de invitación se normaliza", () =>
    {
        Verificar(GestionMembresiasSegura.NormalizarCorreo(" test@example.com ") ==
                 "TEST@EXAMPLE.COM");
        return Task.CompletedTask;
    }),
    ("Correo inválido no permite invitaciones", () =>
    {
        try
        {
            GestionMembresiasSegura.NormalizarCorreo("no-es-correo");
            throw new Exception("Aceptó un correo inválido.");
        }
        catch (InvalidOperationException)
        {
            return Task.CompletedTask;
        }
    }),
    ("No hay endpoint de invitación que eluda el filtro por POST comercial", () =>
    {
        Verificar(ReglasAccesoControladores.ObtenerPermiso(
            "Invitaciones", "Crear", "POST") == null);
        Verificar(ReglasAccesoControladores.ObtenerPermiso(
            "Invitaciones", "CambiarRol", "POST") == null);
        return Task.CompletedTask;
    }),
    ("El propietario no obtiene permisos nuevos por un rol desconocido", () =>
    {
        Verificar(!PermisosNexora.TienePermiso("Propietario", "Nexora.AdministrarTodosLosSistemas"));
        return Task.CompletedTask;
    }),
    ("La bandeja de invitaciones requiere permiso empresarial", () =>
    {
        var permiso = ReglasAccesoControladores.ObtenerPermiso(
            "Equipo", "Invitaciones", "GET");
        Verificar(permiso == PermisosNexora.AdministrarUsuarios);
        Verificar(PermisosNexora.TienePermiso("Propietario", permiso!));
        return Task.CompletedTask;
    }),
    ("Cajero y administrador no pueden ver invitaciones", () =>
    {
        var permiso = ReglasAccesoControladores.ObtenerPermiso(
            "Equipo", "Invitaciones", "GET");
        Verificar(!PermisosNexora.TienePermiso("Cajero", permiso!));
        Verificar(!PermisosNexora.TienePermiso("Administrador", permiso!));
        return Task.CompletedTask;
    }),
    ("Revocar requiere POST y permiso de propietario", () =>
    {
        var permiso = ReglasAccesoControladores.ObtenerPermiso(
            "Equipo", "RevocarInvitacion", "POST");
        Verificar(permiso == PermisosNexora.AdministrarUsuarios);
        Verificar(PermisosNexora.TienePermiso("Propietario", permiso!));
        return Task.CompletedTask;
    }),
    ("Revocación por GET está prohibida", () =>
    {
        Verificar(ReglasAccesoControladores.ObtenerPermiso(
            "Equipo", "RevocarInvitacion", "GET") == null);
        return Task.CompletedTask;
    }),
    ("No se conceden permisos a operaciones nuevas sobre invitaciones", () =>
    {
        foreach (var accion in new[] { "EliminarInvitacion", "ReactivarInvitacion",
                                       "ReenviarToken", "VerToken" })
        {
            Verificar(ReglasAccesoControladores.ObtenerPermiso(
                "Equipo", accion, "POST") == null);
        }
        return Task.CompletedTask;
    }),
    ("Los filtros de invitaciones aceptan únicamente estados definidos", () =>
    {
        foreach (var estado in new[] { "Pendiente", "Vencida", "Aceptada",
                                       "Revocada", "Caducada", "Todas" })
            Verificar(GestionMembresiasSegura.EstadoInvitacionPermitido(estado));
        return Task.CompletedTask;
    }),
    ("Filtros de estado de invitaciones arbitrarios se rechazan", () =>
    {
        Verificar(!GestionMembresiasSegura.EstadoInvitacionPermitido(null));
        Verificar(!GestionMembresiasSegura.EstadoInvitacionPermitido("Propietario"));
        Verificar(!GestionMembresiasSegura.EstadoInvitacionPermitido("Pendiente'; DROP TABLE"));
        return Task.CompletedTask;
    })
};

foreach (var (nombre, ejecutar) in pruebas)
{
    try
    {
        await ejecutar();
        Console.WriteLine("OK  " + nombre);
    }
    catch (Exception error)
    {
        fallos++;
        Console.Error.WriteLine("ERROR  " + nombre + ": " + error.Message);
    }
}

Console.WriteLine($"Pruebas: {pruebas.Length}; errores: {fallos}");
return fallos == 0 ? 0 : 1;

static async Task<ContextoEmpresaVerificado> CrearContextoValidado(string rol)
{
    var resultado = new ContextoEmpresaVerificado();
    var validador = new ValidadorFalso(
        new MembresiaEmpresaValidada(Guid.NewGuid(), rol));
    await Middleware().InvokeAsync(HttpAutenticado("tienda-a"), resultado, validador);
    return resultado;
}

static VerificacionMembresiaMiddleware Middleware()
    => new(_ => Task.CompletedTask);

static DefaultHttpContext HttpAutenticado(string? slug = null)
{
    var http = new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "usuario-prueba") },
            authenticationType: "PruebaIdentity"))
    };
    if (slug != null)
        http.Request.RouteValues = new RouteValueDictionary { ["empresaSlug"] = slug };
    return http;
}

static DefaultHttpContext HttpAnonimo(string slug)
{
    var http = new DefaultHttpContext();
    http.Request.RouteValues = new RouteValueDictionary { ["empresaSlug"] = slug };
    return http;
}

static void Verificar(bool condicion)
{
    if (!condicion)
        throw new Exception("Condición de seguridad incumplida.");
}

sealed class ValidadorFalso : IValidadorAccesoEmpresa
{
    private readonly MembresiaEmpresaValidada? _resultado;
    public int Llamadas { get; private set; }

    public ValidadorFalso(MembresiaEmpresaValidada? resultado)
    {
        _resultado = resultado;
    }

    public Task<MembresiaEmpresaValidada?> ValidarAsync(
        ClaimsPrincipal? usuario,
        string? slugCandidato,
        CancellationToken cancellationToken = default)
    {
        Llamadas++;
        return Task.FromResult(_resultado);
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SistemaFacturacion.Security;
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

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Http.Lab;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models.SaaS;

namespace Nexora.Http.Lab;

// Evitar el Program implícito de sentencias top-level en la herramienta
// de pruebas: WebApplicationFactory<global::Program> debe resolver el Program
// PUBLICO de la aplicación web, nunca el ejecutable de test.
public static class HttpLabRunner
{
    public static async Task<int> Main(string[] args)
    {
// HTTP real a TestServer con cookies reales ASP.NET Core Identity.
// No levanta puertos ni llama servidores externos.
// La única base aceptada es el laboratorio SQL local, VACÍO y autorizado.
string connection;
try
{
    connection = LabSqlGuard.GetVerifiedConnection();
    await LabSqlGuard.VerifyEmptyAsync(connection);
}
catch (Exception error)
{
    Console.Error.WriteLine("STOP SIN ESCRIBIR DATOS: " + error.Message);
    return 2;
}

var sufijo = Guid.NewGuid().ToString("N")[..12];
var tenantA = Guid.NewGuid();
var tenantB = Guid.NewGuid();
var slugA = "http-lab-a-" + sufijo;
var slugB = "http-lab-b-" + sufijo;
var userAId = "http-lab-a-" + sufijo;
var userBId = "http-lab-b-" + sufijo;
var emailA = $"http-a-{sufijo}@example.invalid";
var emailB = $"http-b-{sufijo}@example.invalid";
var clave = "Nexora!HttpTest9-" + Guid.NewGuid().ToString("N");
var inicioFixture = false;
var ejecutadas = 0;
var fallos = 0;

try
{
    using var factory = new WebApplicationFactory<global::Program>()
        .WithWebHostBuilder(web =>
        {
            // Identity autentica con cookies reales, pero SQL queda limitado
            // a NexoraIsolationTest, y ningún puerto se expone al exterior.
            web.UseEnvironment("Development");
            web.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connection,
                    ["Seguridad:ActivarTenancyValidada"] = "true"
                }));
        });

    var anon = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });
    // Desde aquí si ocurre un fallo de creación inicial, limpiar todo lo
    // que pudiera haberse insertado antes de ese fallo.
    inicioFixture = true;
    using (var scope = factory.Services.CreateScope())
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        await CrearIdentidadAsync(users, userAId, emailA, clave);
        await CrearIdentidadAsync(users, userBId, emailB, clave);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Empresas.AddRange(
            new Empresa
            {
                IdEmpresa = tenantA, SlugNormalizado = slugA,
                NombreComercial = "Laboratorio HTTP A",
                Moneda = "DOP", Estado = "Activa",
                ZonaHoraria = "America/Santo_Domingo"
            },
            new Empresa
            {
                IdEmpresa = tenantB, SlugNormalizado = slugB,
                NombreComercial = "Laboratorio HTTP B",
                Moneda = "DOP", Estado = "Activa",
                ZonaHoraria = "America/Santo_Domingo"
            });
        db.EmpresaUsuarios.AddRange(
            new EmpresaUsuario
            {
                IdEmpresa = tenantA, UserId = userAId,
                Rol = "Cajero", Estado = "Activo"
            },
            new EmpresaUsuario
            {
                IdEmpresa = tenantB, UserId = userBId,
                Rol = "Propietario", Estado = "Activo"
            });
        await db.SaveChangesAsync();
    }

    var clientA = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });
    var clientB = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    async Task Probar(string nombre, Func<Task> accion)
    {
        ejecutadas++;
        try
        {
            await accion();
            Console.WriteLine("OK  " + nombre);
        }
        catch (Exception e)
        {
            fallos++;
            Console.Error.WriteLine("ERROR  " + nombre + ": " + e.Message);
        }
    }

    await Probar("Anónimo en Mis empresas es enviado a iniciar sesión", async () =>
    {
        using var r = await anon.GetAsync("/mis-empresas");
        Exigir(EsLogin(r));
    });

    await Probar("Anónimo no abre facturas empresariales", async () =>
    {
        using var r = await anon.GetAsync($"/e/{slugA}/Facturas/Index");
        Exigir(EsLogin(r));
    });

    // Sesiones reales de Identity: GET token anti-CSRF + POST Login.
    await IniciarSesionAsync(clientA, emailA, clave);
    await IniciarSesionAsync(clientB, emailB, clave);

    await Probar("La cookie de A abre Mis empresas con empresa A", async () =>
    {
        using var r = await clientA.GetAsync("/mis-empresas");
        var html = await r.Content.ReadAsStringAsync();
        Exigir(r.StatusCode == HttpStatusCode.OK &&
               html.Contains(slugA, StringComparison.Ordinal) &&
               !html.Contains(slugB, StringComparison.Ordinal));
    });

    await Probar("La cookie de B no ve empresa A en el selector", async () =>
    {
        using var r = await clientB.GetAsync("/mis-empresas");
        var html = await r.Content.ReadAsStringAsync();
        Exigir(r.StatusCode == HttpStatusCode.OK &&
               html.Contains(slugB, StringComparison.Ordinal) &&
               !html.Contains(slugA, StringComparison.Ordinal));
    });

    await Probar("Cajero A abre su dashboard por HTTP", async () =>
    {
        using var r = await clientA.GetAsync($"/e/{slugA}/Home/Index");
        Exigir(r.StatusCode == HttpStatusCode.OK);
    });

    await Probar("Propietario B abre su dashboard por HTTP", async () =>
    {
        using var r = await clientB.GetAsync($"/e/{slugB}/Home/Index");
        Exigir(r.StatusCode == HttpStatusCode.OK);
    });

    await Probar("Cookie de A no permite navegar al dashboard de B", async () =>
    {
        using var r = await clientA.GetAsync($"/e/{slugB}/Home/Index");
        Exigir(EsAccesoDenegado(r));
    });

    await Probar("Cookie de B no permite navegar al dashboard de A", async () =>
    {
        using var r = await clientB.GetAsync($"/e/{slugA}/Home/Index");
        Exigir(EsAccesoDenegado(r));
    });

    await Probar("La ruta comercial sin slug no permite acceso", async () =>
    {
        using var r = await clientA.GetAsync("/Facturas/Index");
        Exigir(EsAccesoDenegado(r));
    });

    await Probar("Cajero A consulta clientes solo dentro de A", async () =>
    {
        using var r = await clientA.GetAsync($"/e/{slugA}/Clientes/Index");
        Exigir(r.StatusCode == HttpStatusCode.OK);
    });

    await Probar("Cajero A no abre el directorio de empleados", async () =>
    {
        using var r = await clientA.GetAsync($"/e/{slugA}/Equipo/Index");
        Exigir(EsAccesoDenegado(r));
    });

    await Probar("Propietario B abre Mi equipo", async () =>
    {
        using var r = await clientB.GetAsync($"/e/{slugB}/Equipo/Index");
        var html = await r.Content.ReadAsStringAsync();
        Exigir(r.StatusCode == HttpStatusCode.OK &&
               html.Contains(emailB, StringComparison.OrdinalIgnoreCase) &&
               !html.Contains(emailA, StringComparison.OrdinalIgnoreCase));
    });

    await Probar("Cajero A no puede anular por POST", async () =>
    {
        using var r = await clientA.PostAsync(
            $"/e/{slugA}/Facturas/Anular/123456", new FormUrlEncodedContent(
                new Dictionary<string, string>()));
        Exigir(EsAccesoDenegado(r));
    });

    await Probar("Propietario B no puede escribir equipo sin antiforgery", async () =>
    {
        using var r = await clientB.PostAsync(
            $"/e/{slugB}/Equipo/CrearInvitacion",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["correo"] = "no-debe-crearse@example.invalid",
                ["rol"] = "Cajero"
            }));
        Exigir(r.StatusCode == HttpStatusCode.BadRequest);
    });

    await Probar("Suspensión SQL invalida inmediatamente la cookie de A", async () =>
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var miembro = await db.EmpresaUsuarios.SingleAsync(m =>
            m.IdEmpresa == tenantA && m.UserId == userAId);
        miembro.Estado = "Suspendido";
        await db.SaveChangesAsync();
        try
        {
            using var r = await clientA.GetAsync($"/e/{slugA}/Home/Index");
            Exigir(EsAccesoDenegado(r));
        }
        finally
        {
            miembro.Estado = "Activo";
            await db.SaveChangesAsync();
        }
    });

    await Probar("Empresa B suspendida bloquea cookie propietaria", async () =>
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var empresa = await db.Empresas.SingleAsync(e => e.IdEmpresa == tenantB);
        empresa.Estado = "Suspendida";
        await db.SaveChangesAsync();
        try
        {
            using var r = await clientB.GetAsync($"/e/{slugB}/Equipo/Index");
            Exigir(EsAccesoDenegado(r));
        }
        finally
        {
            empresa.Estado = "Activa";
            await db.SaveChangesAsync();
        }
    });
}
catch (Exception e)
{
    fallos++;
    Console.Error.WriteLine("ERROR de inicio HTTP: " + e.Message);
}
finally
{
    if (inicioFixture)
    {
        try
        {
            await LabSqlGuard.CleanupAsync(connection,
                tenantA, tenantB, userAId, userBId);
            Console.WriteLine("LIMPIEZA HTTP OK: identidades y empresas ficticias retiradas.");
        }
        catch (Exception e)
        {
            fallos++;
            Console.Error.WriteLine("ALERTA: limpieza HTTP falló: " + e.Message);
            Console.Error.WriteLine("NO repetir ni reutilizar laboratorio hasta verificar filas LAB.");
        }
    }
}

Console.WriteLine($"HTTP Identity: {ejecutadas} verificaciones, {fallos} errores.");
return fallos == 0 && ejecutadas == 16 ? 0 : 1;
    }

    private static async Task CrearIdentidadAsync(
    UserManager<IdentityUser> manager, string id, string email, string password)
{
    var user = new IdentityUser
    {
        Id = id, UserName = email, Email = email, EmailConfirmed = true
    };
    var result = await manager.CreateAsync(user, password);
    if (!result.Succeeded)
        throw new InvalidOperationException("Error al crear usuario Identity de laboratorio: " +
            string.Join("; ", result.Errors.Select(e => e.Code)));
}

private static async Task IniciarSesionAsync(
    HttpClient client, string email, string password)
{
    using var landing = await client.GetAsync("/Identity/Account/Login");
    if (landing.StatusCode != HttpStatusCode.OK)
        throw new InvalidOperationException(
            "No se pudo abrir la pantalla de Identity Login.");
    var html = await landing.Content.ReadAsStringAsync();
    var token = ExtraerAntiforgery(html);
    using var resultado = await client.PostAsync("/Identity/Account/Login",
        new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.RememberMe"] = "false",
            ["__RequestVerificationToken"] = token
        }));
    if (resultado.StatusCode != HttpStatusCode.Redirect &&
        resultado.StatusCode != HttpStatusCode.RedirectMethod &&
        resultado.StatusCode != HttpStatusCode.SeeOther)
        throw new InvalidOperationException(
            "Identity no inició sesión; comprobar configuración de Login / antiforgery.");
}

private static string ExtraerAntiforgery(string html)
{
    foreach (Match input in Regex.Matches(html,
        @"<input\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
    {
        if (!Regex.IsMatch(input.Value,
            @"\bname\s*=\s*['""]__RequestVerificationToken['""]",
            RegexOptions.IgnoreCase))
            continue;
        var value = Regex.Match(input.Value,
            @"\bvalue\s*=\s*['""](?<value>[^'""]+)['""]",
            RegexOptions.IgnoreCase);
        if (value.Success)
            return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }
    throw new InvalidOperationException("No se encontró antiforgery en la página Identity Login.");
}

private static bool EsLogin(HttpResponseMessage r) =>
    r.StatusCode == HttpStatusCode.Redirect &&
    r.Headers.Location?.OriginalString.Contains(
        "/Identity/Account/Login", StringComparison.OrdinalIgnoreCase) == true;

private static bool EsAccesoDenegado(HttpResponseMessage r) =>
    r.StatusCode == HttpStatusCode.Forbidden ||
    (r.StatusCode == HttpStatusCode.Redirect &&
     r.Headers.Location?.OriginalString.Contains(
         "AccessDenied", StringComparison.OrdinalIgnoreCase) == true);

private static void Exigir(bool condicion)
{
    if (!condicion)
        throw new InvalidOperationException("Condición HTTP esperada incumplida.");
}
}

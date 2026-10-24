using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models;
using SistemaFacturacion.Models.SaaS;
using SistemaFacturacion.Security;
using SistemaFacturacion.Services.Tenancy;

// INTEGRACIÓN SQL REAL. No es un test unitario ni realiza login con cookies.
// Exige una base local con nombre exacto y una marca creada intencionalmente.
// Nunca ejecuta migraciones ni crea bases; genera datos ficticios y los retira.
var cadena = Environment.GetEnvironmentVariable("NEXORA_ISOLATION_TEST_CONNECTION");
if (string.IsNullOrWhiteSpace(cadena))
{
    Console.Error.WriteLine("STOP: Falta NEXORA_ISOLATION_TEST_CONNECTION.");
    return 2;
}

SqlConnectionStringBuilder parametros;
try
{
    parametros = new SqlConnectionStringBuilder(cadena);
}
catch (ArgumentException)
{
    Console.Error.WriteLine("STOP: La cadena SQL de laboratorio no es válida.");
    return 2;
}

if (!string.Equals(parametros.InitialCatalog, "NexoraIsolationTest",
        StringComparison.OrdinalIgnoreCase) ||
    !EsServidorLocal(parametros.DataSource))
{
    Console.Error.WriteLine(
        "STOP: Solo se acepta la base NexoraIsolationTest en SQL Server LOCAL.");
    return 2;
}

try
{
    await ConfirmarAislamientoAsync(cadena);
}
catch (Exception ex)
{
    Console.Error.WriteLine("STOP SIN ESCRIBIR DATOS: " + ex.Message);
    return 2;
}

var sufijo = Guid.NewGuid().ToString("N")[..12];
var empresaAId = Guid.NewGuid();
var empresaBId = Guid.NewGuid();
var usuarioAId = "lab-a-" + sufijo;
var usuarioBId = "lab-b-" + sufijo;
var slugA = "lab-a-" + sufijo;
var slugB = "lab-b-" + sufijo;
var marcaCliente = "Lab" + sufijo;
var usuarioA = Principal(usuarioAId);
var usuarioB = Principal(usuarioBId);
var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(cadena).Options;
var configuracion = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = cadena
    }).Build();
var verificador = new ValidadorAccesoEmpresaSql(configuracion);
var fallos = 0;
var ejecutadas = 0;
var fixtureIniciada = false;

ApplicationDbContext Contexto(Guid id) =>
    new(opciones, new TenantPrueba(id));

async Task VerificarAsync(string titulo, Func<Task> prueba)
{
    ejecutadas++;
    try
    {
        await prueba();
        Console.WriteLine("OK  " + titulo);
    }
    catch (Exception ex)
    {
        fallos++;
        Console.Error.WriteLine("ERROR  " + titulo + ": " + ex.Message);
    }
}

try
{
    fixtureIniciada = true;
    await using (var global = Contexto(Guid.Empty))
    {
        global.Users.AddRange(
            UsuarioFicticio(usuarioAId, sufijo, "a"),
            UsuarioFicticio(usuarioBId, sufijo, "b"));
        global.Empresas.AddRange(
            EmpresaFicticia(empresaAId, slugA, "Laboratorio A"),
            EmpresaFicticia(empresaBId, slugB, "Laboratorio B"));
        global.EmpresaUsuarios.AddRange(
            new EmpresaUsuario
            {
                IdEmpresa = empresaAId, UserId = usuarioAId,
                Rol = "Cajero", Estado = "Activo"
            },
            new EmpresaUsuario
            {
                IdEmpresa = empresaBId, UserId = usuarioBId,
                Rol = "Propietario", Estado = "Activo"
            });
        await global.SaveChangesAsync();
    }

    await using (var a = Contexto(empresaAId))
    {
        a.Clientes.Add(new Cliente { Nombre = marcaCliente, Apellido = "A" });
        await a.SaveChangesAsync();
    }
    await using (var b = Contexto(empresaBId))
    {
        b.Clientes.Add(new Cliente { Nombre = marcaCliente, Apellido = "B" });
        await b.SaveChangesAsync();
    }

    await VerificarAsync("Anónimo no obtiene membresía", async () =>
    {
        var resultado = await verificador.ValidarAsync(
            new ClaimsPrincipal(new ClaimsIdentity()), slugA);
        Exigir(resultado == null);
    });

    await VerificarAsync("Usuario A obtiene únicamente empresa A", async () =>
    {
        var resultado = await verificador.ValidarAsync(usuarioA, slugA);
        Exigir(resultado?.IdEmpresa == empresaAId && resultado.Rol == "Cajero");
    });

    await VerificarAsync("Usuario B obtiene únicamente empresa B", async () =>
    {
        var resultado = await verificador.ValidarAsync(usuarioB, slugB);
        Exigir(resultado?.IdEmpresa == empresaBId && resultado.Rol == "Propietario");
    });

    await VerificarAsync("Usuario A intentando empresa B es rechazado", async () =>
    {
        Exigir(await verificador.ValidarAsync(usuarioA, slugB) == null);
    });

    await VerificarAsync("Usuario B intentando empresa A es rechazado", async () =>
    {
        Exigir(await verificador.ValidarAsync(usuarioB, slugA) == null);
    });

    await VerificarAsync("Empresa con slug ajeno es rechazada", async () =>
    {
        Exigir(await verificador.ValidarAsync(usuarioA, "empresa-inexistente") == null);
    });

    await VerificarAsync("Middleware asigna empresa validada A", async () =>
    {
        var contexto = await ResolverAsync(usuarioA, slugA, verificador);
        Exigir(contexto.IdEmpresa == empresaAId && contexto.RolEmpresa == "Cajero");
    });

    await VerificarAsync("Middleware no valida empresa B para usuario A", async () =>
    {
        var contexto = await ResolverAsync(usuarioA, slugB, verificador);
        Exigir(!contexto.TieneEmpresa && contexto.IdEmpresa == Guid.Empty);
    });

    await VerificarAsync("Cajero puede crear factura, con empresa A", async () =>
    {
        var contexto = await ResolverAsync(usuarioA, slugA, verificador);
        Exigir(await PermisoAsync(contexto, usuarioA, PermisosNexora.CrearFacturas));
    });

    await VerificarAsync("Cajero no puede anular factura", async () =>
    {
        var contexto = await ResolverAsync(usuarioA, slugA, verificador);
        Exigir(!await PermisoAsync(contexto, usuarioA, PermisosNexora.AnularFacturas));
    });

    await VerificarAsync("Cajero no puede administrar empleados", async () =>
    {
        var contexto = await ResolverAsync(usuarioA, slugA, verificador);
        Exigir(!await PermisoAsync(contexto, usuarioA, PermisosNexora.AdministrarUsuarios));
    });

    await VerificarAsync("Propietario B puede consultar su equipo", async () =>
    {
        var contexto = await ResolverAsync(usuarioB, slugB, verificador);
        Exigir(await PermisoAsync(contexto, usuarioB, PermisosNexora.AdministrarUsuarios));
    });

    await VerificarAsync("El filtro EF de A oculta clientes de B", async () =>
    {
        await using var db = Contexto(empresaAId);
        var clientes = await db.Clientes.Where(c => c.Nombre == marcaCliente).ToListAsync();
        Exigir(clientes.Count == 1 && clientes[0].Apellido == "A");
    });

    await VerificarAsync("El filtro EF de B oculta clientes de A", async () =>
    {
        await using var db = Contexto(empresaBId);
        var clientes = await db.Clientes.Where(c => c.Nombre == marcaCliente).ToListAsync();
        Exigir(clientes.Count == 1 && clientes[0].Apellido == "B");
    });

    await VerificarAsync("Contexto sin empresa no ve clientes", async () =>
    {
        await using var db = Contexto(Guid.Empty);
        Exigir(!await db.Clientes.AnyAsync(c => c.Nombre == marcaCliente));
    });

    await VerificarAsync("No se puede insertar cliente de B desde A", async () =>
    {
        await using var db = Contexto(empresaAId);
        db.Clientes.Add(new Cliente
        {
            Nombre = marcaCliente,
            Apellido = "CruceProhibido",
            IdEmpresa = empresaBId
        });
        try
        {
            await db.SaveChangesAsync();
            throw new Exception("Se escribió un cliente interempresarial.");
        }
        catch (UnauthorizedAccessException)
        {
            // Comportamiento esperado del guard de ApplicationDbContext.
        }
    });

    await VerificarAsync("Sin tenant no es posible escribir datos comerciales", async () =>
    {
        await using var db = Contexto(Guid.Empty);
        db.Clientes.Add(new Cliente { Nombre = marcaCliente, Apellido = "SinTenant" });
        try
        {
            await db.SaveChangesAsync();
            throw new Exception("Se escribió un cliente sin tenant.");
        }
        catch (UnauthorizedAccessException)
        {
            // Guard de fail-closed.
        }
    });

    await VerificarAsync("Suspender membresía A revoca acceso inmediatamente", async () =>
    {
        await CambiarMembresiaAsync("Suspendido");
        try
        {
            Exigir(await verificador.ValidarAsync(usuarioA, slugA) == null);
        }
        finally
        {
            await CambiarMembresiaAsync("Activo");
        }
        Exigir((await verificador.ValidarAsync(usuarioA, slugA))?.IdEmpresa == empresaAId);
    });

    await VerificarAsync("Baja de membresía A revoca acceso inmediatamente", async () =>
    {
        await using (var db = Contexto(Guid.Empty))
        {
            var m = await db.EmpresaUsuarios.SingleAsync(x =>
                x.IdEmpresa == empresaAId && x.UserId == usuarioAId);
            m.FechaBajaUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        try
        {
            Exigir(await verificador.ValidarAsync(usuarioA, slugA) == null);
        }
        finally
        {
            await using var db = Contexto(Guid.Empty);
            var m = await db.EmpresaUsuarios.SingleAsync(x =>
                x.IdEmpresa == empresaAId && x.UserId == usuarioAId);
            m.FechaBajaUtc = null;
            await db.SaveChangesAsync();
        }
    });

    await VerificarAsync("Empresa A suspendida revoca la pertenencia", async () =>
    {
        await CambiarEstadoEmpresaAsync("Suspendida");
        try
        {
            Exigir(await verificador.ValidarAsync(usuarioA, slugA) == null);
            Exigir((await verificador.ValidarAsync(usuarioB, slugB))?.IdEmpresa ==
                   empresaBId);
        }
        finally
        {
            await CambiarEstadoEmpresaAsync("Activa");
        }
    });

    await VerificarAsync("Miembros activos no se confunden entre empresas", async () =>
    {
        await using var db = Contexto(Guid.Empty);
        var paraA = await db.EmpresaUsuarios.Where(m => m.IdEmpresa == empresaAId &&
            m.Estado == "Activo").Select(m => m.UserId).ToListAsync();
        var paraB = await db.EmpresaUsuarios.Where(m => m.IdEmpresa == empresaBId &&
            m.Estado == "Activo").Select(m => m.UserId).ToListAsync();
        Exigir(paraA.Count == 1 && paraA[0] == usuarioAId);
        Exigir(paraB.Count == 1 && paraB[0] == usuarioBId);
    });

    async Task CambiarMembresiaAsync(string estado)
    {
        await using var db = Contexto(Guid.Empty);
        var m = await db.EmpresaUsuarios.SingleAsync(x =>
            x.IdEmpresa == empresaAId && x.UserId == usuarioAId);
        m.Estado = estado;
        await db.SaveChangesAsync();
    }

    async Task CambiarEstadoEmpresaAsync(string estado)
    {
        await using var db = Contexto(Guid.Empty);
        var e = await db.Empresas.SingleAsync(x => x.IdEmpresa == empresaAId);
        e.Estado = estado;
        await db.SaveChangesAsync();
    }
}
catch (Exception ex)
{
    fallos++;
    Console.Error.WriteLine("ERROR de preparación/ejecución: " + ex.Message);
}
finally
{
    if (fixtureIniciada)
    {
        try
        {
            await LimpiarFixtureAsync(cadena, empresaAId, empresaBId,
                usuarioAId, usuarioBId, marcaCliente);
            Console.WriteLine("LIMPIEZA OK: se retiraron los datos ficticios de esta ejecución.");
        }
        catch (Exception ex)
        {
            fallos++;
            Console.Error.WriteLine(
                "ALERTA: fallo la limpieza del laboratorio: " + ex.Message);
            Console.Error.WriteLine(
                "NO reutilizar la base hasta revisar los registros LAB manualmente.");
        }
    }
}

Console.WriteLine($"Integración SQL: {ejecutadas} verificaciones, {fallos} errores.");
return fallos == 0 && ejecutadas == 21 ? 0 : 1;

static bool EsServidorLocal(string servidor)
{
    var s = servidor.Trim().ToLowerInvariant();
    return s == "." || s == "(local)" || s == "localhost" ||
           s == "127.0.0.1" || s == "::1" ||
           s.StartsWith(@"(localdb)\", StringComparison.Ordinal) ||
           s.StartsWith(@".\", StringComparison.Ordinal) ||
           s.StartsWith(@"localhost\", StringComparison.Ordinal) ||
           s.StartsWith(@"127.0.0.1\", StringComparison.Ordinal) ||
           s.StartsWith("localhost,", StringComparison.Ordinal) ||
           s.StartsWith("127.0.0.1,", StringComparison.Ordinal);
}

static async Task ConfirmarAislamientoAsync(string cadena)
{
    await using var c = new SqlConnection(cadena);
    await c.OpenAsync();
    const string sql = @"
IF DB_NAME() <> N'NexoraIsolationTest'
    THROW 51040, 'La base no es el laboratorio esperado.', 1;
IF OBJECT_ID(N'dbo.NexoraIsolationLabGuard', N'U') IS NULL
    THROW 51041, 'Falta la marca manual del laboratorio.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.NexoraIsolationLabGuard
               WHERE Marca = N'NEXORA_EXPLICIT_LAB_ONLY')
    THROW 51042, 'Marca de laboratorio no autorizada.', 1;
IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL OR
   OBJECT_ID(N'dbo.EmpresaUsuarios', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Clientes', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Categorias', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Productos', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Facturas', N'U') IS NULL OR
   OBJECT_ID(N'dbo.DetallesFactura', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Pagos', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Empresas', N'U') IS NULL
    THROW 51043, 'Faltan migraciones de Nexora en la base aislada.', 1;
IF EXISTS (SELECT 1 FROM dbo.AspNetUsers) OR
   EXISTS (SELECT 1 FROM dbo.EmpresaUsuarios) OR
   EXISTS (SELECT 1 FROM dbo.Clientes) OR
   EXISTS (SELECT 1 FROM dbo.Categorias) OR
   EXISTS (SELECT 1 FROM dbo.Productos) OR
   EXISTS (SELECT 1 FROM dbo.Facturas) OR
   EXISTS (SELECT 1 FROM dbo.DetallesFactura) OR
   EXISTS (SELECT 1 FROM dbo.Pagos)
    THROW 51044, 'Laboratorio con datos previos; requiere base limpia, no escribir.', 1;
SELECT 1;";
    await using var cmd = new SqlCommand(sql, c);
    Exigir(Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 1);
}

static ClaimsPrincipal Principal(string userId) =>
    new(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "IntegracionNexora"));

static IdentityUser UsuarioFicticio(string id, string sufijo, string inicial)
{
    var name = $"test-{inicial}-{sufijo}@example.invalid";
    return new IdentityUser
    {
        Id = id, UserName = name, NormalizedUserName = name.ToUpperInvariant(),
        Email = name, NormalizedEmail = name.ToUpperInvariant(),
        EmailConfirmed = true, SecurityStamp = Guid.NewGuid().ToString("N")
    };
}

static Empresa EmpresaFicticia(Guid id, string slug, string nombre) => new()
{
    IdEmpresa = id, SlugNormalizado = slug, NombreComercial = nombre,
    Estado = "Activa", Moneda = "DOP", ZonaHoraria = "America/Santo_Domingo"
};

static async Task<ContextoEmpresaVerificado> ResolverAsync(
    ClaimsPrincipal usuario, string slug, IValidadorAccesoEmpresa verificador)
{
    var contexto = new ContextoEmpresaVerificado();
    var http = new DefaultHttpContext { User = usuario };
    http.Request.RouteValues = new RouteValueDictionary { ["empresaSlug"] = slug };
    await new VerificacionMembresiaMiddleware(_ => Task.CompletedTask)
        .InvokeAsync(http, contexto, verificador);
    return contexto;
}

static async Task<bool> PermisoAsync(
    ContextoEmpresaVerificado empresa, ClaimsPrincipal usuario, string nombre)
{
    var requisito = new PermisoEmpresaRequirement(nombre);
    var ctx = new AuthorizationHandlerContext(new[] { requisito }, usuario, null);
    await new PermisoEmpresaHandler(empresa).HandleAsync(ctx);
    return ctx.HasSucceeded;
}

static void Exigir(bool condicion)
{
    if (!condicion) throw new InvalidOperationException("Condición esperada incumplida.");
}

static async Task LimpiarFixtureAsync(
    string cadena, Guid empresaA, Guid empresaB,
    string usuarioA, string usuarioB, string marca)
{
    await using var c = new SqlConnection(cadena);
    await c.OpenAsync();
    await using var tx = (SqlTransaction)await c.BeginTransactionAsync();
    const string sql = @"
DELETE FROM dbo.Clientes
 WHERE IdEmpresa IN (@EmpresaA, @EmpresaB) AND Nombre = @Marca;
DELETE FROM dbo.EmpresaUsuarios
 WHERE (IdEmpresa = @EmpresaA AND UserId = @UsuarioA)
    OR (IdEmpresa = @EmpresaB AND UserId = @UsuarioB);
DELETE FROM dbo.AspNetUsers WHERE Id IN (@UsuarioA, @UsuarioB);
DELETE FROM dbo.Empresas WHERE IdEmpresa IN (@EmpresaA, @EmpresaB);";
    await using var cmd = new SqlCommand(sql, c, tx);
    cmd.Parameters.Add("@EmpresaA", SqlDbType.UniqueIdentifier).Value = empresaA;
    cmd.Parameters.Add("@EmpresaB", SqlDbType.UniqueIdentifier).Value = empresaB;
    cmd.Parameters.Add("@UsuarioA", SqlDbType.NVarChar, 450).Value = usuarioA;
    cmd.Parameters.Add("@UsuarioB", SqlDbType.NVarChar, 450).Value = usuarioB;
    cmd.Parameters.Add("@Marca", SqlDbType.NVarChar, 50).Value = marca;
    await cmd.ExecuteNonQueryAsync();
    await tx.CommitAsync();
}

sealed class TenantPrueba(Guid empresa) : IContextoEmpresa
{
    public Guid IdEmpresa { get; } = empresa;
    public bool TieneEmpresa => IdEmpresa != Guid.Empty;
}

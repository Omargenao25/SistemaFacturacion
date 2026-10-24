using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models.SaaS;
using SistemaFacturacion.Services.Tenancy;

// PRECAUCIÓN: SOLO para una BD SQL local y DESCARTABLE. No crea bases,
// no migra esquemas y no modifica la base normal de Nexora.
const string basePermitida = "NexoraIsolationTest";
const string marcaLaboratorio = "NEXORA_EXPLICIT_LAB_ONLY";
const string marcaNavegador = "NEXORA_BROWSER_LAB_ACTIVE";
const string dominioFicticio = "@browser.nexora.invalid";
const string slugA = "lab-browser-a";
const string slugB = "lab-browser-b";

if (args.Length != 1 || args[0] is not ("--prepare" or "--cleanup"))
{
    Console.Error.WriteLine("Uso: dotnet run --project tests/Nexora.Browser.Lab -- --prepare | --cleanup");
    return 2;
}

var connection = Environment.GetEnvironmentVariable("NEXORA_ISOLATION_TEST_CONNECTION");
if (string.IsNullOrWhiteSpace(connection))
{
    Console.Error.WriteLine("STOP: Falta NEXORA_ISOLATION_TEST_CONNECTION.");
    return 2;
}

try
{
    var cadena = new SqlConnectionStringBuilder(connection);
    if (!string.Equals(cadena.InitialCatalog, basePermitida, StringComparison.Ordinal) ||
        !EsLocal(cadena.DataSource))
        throw new InvalidOperationException(
            "La base debe ser NexoraIsolationTest y el servidor debe ser LOCAL.");

    await using (var sql = new SqlConnection(connection))
    {
        await sql.OpenAsync();
        const string permisoSql = @"
IF DB_NAME() <> N'NexoraIsolationTest'
    THROW 51101, 'Base inesperada. Abortado.', 1;
IF OBJECT_ID(N'dbo.NexoraIsolationLabGuard', 'U') IS NULL
    THROW 51102, 'No existe la marca del laboratorio.', 1;
IF NOT EXISTS(SELECT 1 FROM dbo.NexoraIsolationLabGuard
              WHERE Marca = N'NEXORA_EXPLICIT_LAB_ONLY')
    THROW 51103, 'Laboratorio no autorizado.', 1;
IF OBJECT_ID(N'dbo.AspNetUsers', 'U') IS NULL OR
   OBJECT_ID(N'dbo.EmpresaUsuarios', 'U') IS NULL OR
   OBJECT_ID(N'dbo.Empresas', 'U') IS NULL OR
   OBJECT_ID(N'dbo.Clientes', 'U') IS NULL OR
   OBJECT_ID(N'dbo.InvitacionesEmpresa', 'U') IS NULL
    THROW 51104, 'Faltan migraciones en la base de laboratorio.', 1;
SELECT 1;";
        await using var cmd = new SqlCommand(permisoSql, sql);
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("Falló la verificación SQL.");
    }

    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer(connection).Options;
    await using var db = new ApplicationDbContext(options, new TenantSinEmpresa());
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

    var existeMarca = await db.Database
        .SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM dbo.NexoraIsolationLabGuard WHERE Marca = N'NEXORA_BROWSER_LAB_ACTIVE'")
        .SingleAsync() != 0;

    if (args[0] == "--prepare")
    {
        if (existeMarca)
            throw new InvalidOperationException(
                "El laboratorio de navegador ya está preparado. No duplicar cuentas.");

        // Requiere base sin cuentas ni registros comerciales de otra prueba.
        // La empresa legacy Inactiva creada por migraciones puede permanecer.
        const string comprobarVacio = @"
SELECT CASE WHEN
    EXISTS(SELECT 1 FROM dbo.AspNetUsers) OR
    EXISTS(SELECT 1 FROM dbo.EmpresaUsuarios) OR
    EXISTS(SELECT 1 FROM dbo.Clientes) OR
    EXISTS(SELECT 1 FROM dbo.Productos) OR
    EXISTS(SELECT 1 FROM dbo.Facturas) OR
    EXISTS(SELECT 1 FROM dbo.Pagos) OR
    EXISTS(SELECT 1 FROM dbo.InvitacionesEmpresa) OR
    EXISTS(SELECT 1 FROM dbo.AuditoriaMembresias)
THEN 1 ELSE 0 END AS Value;";
        if (await db.Database.SqlQueryRaw<int>(comprobarVacio).SingleAsync() != 0)
            throw new InvalidOperationException(
                "La base contiene cuentas o datos previos. Crear otro laboratorio vacío, no borrar nada.");

        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        db.Empresas.AddRange(
            new Empresa
            {
                IdEmpresa = a, SlugNormalizado = slugA,
                NombreComercial = "Laboratorio Navegador A", Estado = "Activa",
                Moneda = "DOP", ZonaHoraria = "America/Santo_Domingo"
            },
            new Empresa
            {
                IdEmpresa = b, SlugNormalizado = slugB,
                NombreComercial = "Laboratorio Navegador B", Estado = "Activa",
                Moneda = "DOP", ZonaHoraria = "America/Santo_Domingo"
            });

        var roles = new (string Prefijo, Guid? Empresa, string? Rol)[]
        {
            ("owner-a", a, "Propietario"),
            ("cashier-a", a, "Cajero"),
            ("owner-b", b, "Propietario"),
            ("no-member", null, null)
        };
        var credenciales = new List<(string Email, string Clave)>();
        var hasher = new PasswordHasher<IdentityUser>();

        foreach (var (prefijo, empresa, rol) in roles)
        {
            var email = prefijo + dominioFicticio;
            var clave = "Nx8!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(18)) + "a";
            var u = new IdentityUser
            {
                Id = "nx-browser-" + prefijo,
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                LockoutEnabled = true
            };
            u.PasswordHash = hasher.HashPassword(u, clave);
            db.Users.Add(u);

            if (empresa.HasValue && rol != null)
                db.EmpresaUsuarios.Add(new EmpresaUsuario
                {
                    IdEmpresa = empresa.Value, UserId = u.Id,
                    Rol = rol, Estado = "Activo"
                });

            credenciales.Add((email, clave));
        }

        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO dbo.NexoraIsolationLabGuard (Marca) VALUES (N'NEXORA_BROWSER_LAB_ACTIVE')");
        await tx.CommitAsync();

        Console.WriteLine("LABORATORIO DE NAVEGADOR PREPARADO (SOLO NexoraIsolationTest).");
        Console.WriteLine("Empresa A: /e/lab-browser-a/Home/Index");
        Console.WriteLine("Empresa B: /e/lab-browser-b/Home/Index");
        Console.WriteLine("CREDENCIALES TEMPORALES, MOSTRADAS UNA SOLA VEZ:");
        foreach (var (email, clave) in credenciales)
            Console.WriteLine("Usuario: " + email + " | Contraseña: " + clave);
        Console.WriteLine("No compartir capturas de estas contraseñas ni publicar el laboratorio.");
        Console.WriteLine("Al finalizar: ejecutar --cleanup (sin registrar ventas ni invitaciones).");
    }
    else
    {
        if (!existeMarca)
            throw new InvalidOperationException(
                "No hay fixture de navegador autorizado para limpiar.");

        var empresas = await db.Empresas
            .Where(e => e.SlugNormalizado == slugA || e.SlugNormalizado == slugB)
            .ToListAsync();
        var usuarios = await db.Users
            .Where(u => u.Email != null && u.Email.EndsWith(dominioFicticio))
            .ToListAsync();
        if (empresas.Count != 2 || usuarios.Count != 4 ||
            usuarios.Any(u => !u.Id.StartsWith("nx-browser-", StringComparison.Ordinal)))
            throw new InvalidOperationException(
                "El fixture no coincide exactamente. No borrar ningún registro.");

        var idsEmpresa = empresas.Select(e => e.IdEmpresa).ToList();
        var idsUsuarios = usuarios.Select(u => u.Id).ToList();
        var miembros = await db.EmpresaUsuarios
            .Where(m => idsEmpresa.Contains(m.IdEmpresa) &&
                        idsUsuarios.Contains(m.UserId))
            .ToListAsync();
        if (miembros.Count != 3)
            throw new InvalidOperationException(
                "Las membresías cambiaron; revisar el laboratorio antes de limpiar.");

        db.EmpresaUsuarios.RemoveRange(miembros);
        db.Users.RemoveRange(usuarios);
        db.Empresas.RemoveRange(empresas);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM dbo.NexoraIsolationLabGuard WHERE Marca = N'NEXORA_BROWSER_LAB_ACTIVE'");
        await tx.CommitAsync();
        Console.WriteLine("LIMPIEZA OK: cuentas y empresas ficticias del navegador eliminadas.");
    }

    return 0;
}
catch (Exception e)
{
    Console.Error.WriteLine("STOP: " + e.Message);
    Console.Error.WriteLine("No modificar otras bases ni saltar los controles de protección.");
    return 1;
}

static bool EsLocal(string servidor)
{
    var s = servidor.Trim().ToLowerInvariant();
    return s is "." or "(local)" or "localhost" or "127.0.0.1" or "::1" ||
        s.StartsWith(@"(localdb)\", StringComparison.Ordinal) ||
        s.StartsWith(@".\", StringComparison.Ordinal) ||
        s.StartsWith(@"localhost\", StringComparison.Ordinal) ||
        s.StartsWith(@"127.0.0.1\", StringComparison.Ordinal) ||
        s.StartsWith("localhost,", StringComparison.Ordinal) ||
        s.StartsWith("127.0.0.1,", StringComparison.Ordinal);
}

sealed class TenantSinEmpresa : IContextoEmpresa
{
    public Guid IdEmpresa => Guid.Empty;
    public bool TieneEmpresa => false;
}

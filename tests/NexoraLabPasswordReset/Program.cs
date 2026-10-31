using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

const string baseDatos = "NexoraSecurityLab_Phase8";
const string conexion = "Server=(localdb)\\mssqllocaldb;Database=NexoraSecurityLab_Phase8;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True";
string[] correos =
[
    "propietario-a@nexora-lab.test",
    "admin-a@nexora-lab.test",
    "cajero-a@nexora-lab.test",
    "propietario-b@nexora-lab.test"
];

Console.WriteLine("NEXORA — RESTABLECIMIENTO LOCAL DE CLAVES (SOLO LAB)");
Console.WriteLine("No escribe contraseñas en GitHub ni en archivos.");
Console.WriteLine("Se cambiarán únicamente las cuatro cuentas ficticias del laboratorio.");
Console.Write("Para confirmar, escribe exactamente RESTABLECER-LAB: ");
if (Console.ReadLine() != "RESTABLECER-LAB")
{
    Console.WriteLine("Cancelado.");
    return;
}

await using var sql = new SqlConnection(conexion);
await sql.OpenAsync();
await using (var comprobar = new SqlCommand("SELECT DB_NAME()", sql))
{
    if ((string?)await comprobar.ExecuteScalarAsync() != baseDatos)
        throw new InvalidOperationException("Base incorrecta; no se modificó nada.");
}

var hasher = new PasswordHasher<IdentityUser>();
foreach (var correo in correos)
{
    Console.WriteLine($"\\nCuenta: {correo}");
    Console.Write("Nueva contraseña (mínimo 12 caracteres, mayúscula, minúscula, número y símbolo): ");
    var clave = LeerOculto();

    if (clave.Length < 12 ||
        !clave.Any(char.IsUpper) || !clave.Any(char.IsLower) ||
        !clave.Any(char.IsDigit) || !clave.Any(c => !char.IsLetterOrDigit(c)))
    {
        Console.WriteLine("Contraseña no válida; cuenta omitida.");
        continue;
    }

    var usuario = new IdentityUser { Email = correo, UserName = correo };
    var hash = hasher.HashPassword(usuario, clave);
    clave = string.Empty;
    var securityStamp = Guid.NewGuid().ToString();
    var concurrencyStamp = Guid.NewGuid().ToString();

    const string comandoTexto = @"
UPDATE dbo.AspNetUsers
SET PasswordHash=@hash,
    SecurityStamp=@securityStamp,
    ConcurrencyStamp=@concurrencyStamp
WHERE NormalizedEmail=@correo
  AND EmailConfirmed=1
  AND DB_NAME()=N'NexoraSecurityLab_Phase8';";

    await using var comando = new SqlCommand(comandoTexto, sql);
    comando.Parameters.Add("@hash", SqlDbType.NVarChar, -1).Value = hash;
    comando.Parameters.Add("@securityStamp", SqlDbType.NVarChar, 450).Value = securityStamp;
    comando.Parameters.Add("@concurrencyStamp", SqlDbType.NVarChar, 450).Value = concurrencyStamp;
    comando.Parameters.Add("@correo", SqlDbType.NVarChar, 256).Value = correo.ToUpperInvariant();
    var filas = await comando.ExecuteNonQueryAsync();
    if (filas != 1)
        throw new InvalidOperationException($"No se actualizó exactamente una cuenta: {correo}. Deteniendo.");
    Console.WriteLine("Contraseña actualizada correctamente.");
}

Console.WriteLine("\\nFinalizado. Reinicia la aplicación e inicia sesión con las nuevas claves.");
Console.WriteLine("No compartas las nuevas contraseñas en el chat.");

static string LeerOculto()
{
    var chars = new List<char>();
    while (true)
    {
        var k = Console.ReadKey(intercept: true);
        if (k.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return new string(chars.ToArray());
        }
        if (k.Key == ConsoleKey.Backspace)
        {
            if (chars.Count > 0) chars.RemoveAt(chars.Count - 1);
            continue;
        }
        if (!char.IsControl(k.KeyChar)) chars.Add(k.KeyChar);
    }
}

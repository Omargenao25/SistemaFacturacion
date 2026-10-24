using System.Data;
using Microsoft.Data.SqlClient;

namespace Nexora.Http.Lab;

/// <summary>
/// NEVER use a regular appsettings connection string.
/// Lab must be local, explicitly named and manually stamped before ANY write.
/// </summary>
public static class LabSqlGuard
{
    public static string GetVerifiedConnection()
    {
        var connection = Environment.GetEnvironmentVariable("NEXORA_ISOLATION_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("STOP: configura NEXORA_ISOLATION_TEST_CONNECTION solo para laboratorio.");

        SqlConnectionStringBuilder parsed;
        try { parsed = new SqlConnectionStringBuilder(connection); }
        catch (ArgumentException) { throw new InvalidOperationException("STOP: cadena SQL no válida."); }

        if (!string.Equals(parsed.InitialCatalog, "NexoraIsolationTest",
                StringComparison.OrdinalIgnoreCase) ||
            !LocalSqlServer(parsed.DataSource))
            throw new InvalidOperationException(
                "STOP: solo SQL Server local y Database=NexoraIsolationTest.");

        // Avoid stale appsettings connection strings, even if a developer sets one.
        return parsed.ConnectionString;
    }

    public static async Task VerifyEmptyAsync(string connection)
    {
        await using var sql = new SqlConnection(connection);
        await sql.OpenAsync();
        const string probe = @"
IF DB_NAME() <> N'NexoraIsolationTest'
    THROW 51080, 'STOP: base incorrecta', 1;
IF OBJECT_ID(N'dbo.NexoraIsolationLabGuard', N'U') IS NULL
    THROW 51081, 'STOP: falta la marca manual de laboratorio', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.NexoraIsolationLabGuard
               WHERE Marca = N'NEXORA_EXPLICIT_LAB_ONLY')
    THROW 51082, 'STOP: laboratorio no autorizado', 1;
IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Empresas', N'U') IS NULL OR
   OBJECT_ID(N'dbo.EmpresaUsuarios', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Clientes', N'U') IS NULL OR
   OBJECT_ID(N'dbo.AuditoriaMembresias', N'U') IS NULL
    THROW 51083, 'STOP: faltan migraciones de Nexora', 1;
IF EXISTS(SELECT 1 FROM dbo.AspNetUsers) OR
   EXISTS(SELECT 1 FROM dbo.EmpresaUsuarios) OR
   EXISTS(SELECT 1 FROM dbo.Clientes) OR
   EXISTS(SELECT 1 FROM dbo.Categorias) OR
   EXISTS(SELECT 1 FROM dbo.Productos) OR
   EXISTS(SELECT 1 FROM dbo.Facturas) OR
   EXISTS(SELECT 1 FROM dbo.DetallesFactura) OR
   EXISTS(SELECT 1 FROM dbo.Pagos) OR
   EXISTS(SELECT 1 FROM dbo.InvitacionesEmpresa) OR
   EXISTS(SELECT 1 FROM dbo.AuditoriaMembresias)
    THROW 51084, 'STOP: el laboratorio contiene usuarios o información comercial', 1;
SELECT 1;";
        await using var cmd = new SqlCommand(probe, sql) { CommandTimeout = 20 };
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("STOP: verificación de laboratorio no concluyente.");
    }

    public static async Task CleanupAsync(
        string connection, Guid companyA, Guid companyB, string userA, string userB)
    {
        await using var sql = new SqlConnection(connection);
        await sql.OpenAsync();
        await using var tx = (SqlTransaction)await sql.BeginTransactionAsync(IsolationLevel.Serializable);
        const string statement = @"
DELETE FROM dbo.EmpresaUsuarios
WHERE (IdEmpresa = @A AND UserId = @UserA)
   OR (IdEmpresa = @B AND UserId = @UserB);
DELETE FROM dbo.AspNetUsers WHERE Id IN (@UserA, @UserB);
DELETE FROM dbo.Empresas WHERE IdEmpresa IN (@A, @B);";
        await using var cmd = new SqlCommand(statement, sql, tx);
        cmd.Parameters.Add("@A", SqlDbType.UniqueIdentifier).Value = companyA;
        cmd.Parameters.Add("@B", SqlDbType.UniqueIdentifier).Value = companyB;
        cmd.Parameters.Add("@UserA", SqlDbType.NVarChar, 450).Value = userA;
        cmd.Parameters.Add("@UserB", SqlDbType.NVarChar, 450).Value = userB;
        await cmd.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }

    private static bool LocalSqlServer(string server)
    {
        var s = server.Trim().ToLowerInvariant();
        return s is "." or "(local)" or "localhost" or "127.0.0.1" or "::1" ||
               s.StartsWith(@"(localdb)\", StringComparison.Ordinal) ||
               s.StartsWith(@".\", StringComparison.Ordinal) ||
               s.StartsWith(@"localhost\", StringComparison.Ordinal) ||
               s.StartsWith(@"127.0.0.1\", StringComparison.Ordinal) ||
               s.StartsWith("localhost,", StringComparison.Ordinal) ||
               s.StartsWith("127.0.0.1,", StringComparison.Ordinal);
    }
}

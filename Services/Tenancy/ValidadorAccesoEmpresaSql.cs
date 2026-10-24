using System.Data;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using SistemaFacturacion.Security;

namespace SistemaFacturacion.Services.Tenancy
{
    /// <summary>
    /// Consulta SOLO el catálogo global de empresas y membresías.
    /// No depende de ApplicationDbContext; evita la dependencia circular
    /// ApplicationDbContext -> IContextoEmpresa -> ApplicationDbContext.
    ///
    /// Aún no participa en el pipeline MVC. No activar multiempresa hasta
    /// integrarlo con autenticación, autorizaciones y pruebas A/B.
    /// </summary>
    public sealed class ValidadorAccesoEmpresaSql : IValidadorAccesoEmpresa
    {
        private readonly string _connectionString;

        public ValidadorAccesoEmpresaSql(IConfiguration configuracion)
        {
            _connectionString = configuracion.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Falta la cadena de conexión DefaultConnection.");
        }

        public async Task<MembresiaEmpresaValidada?> ValidarAsync(
            ClaimsPrincipal? usuario,
            string? slugCandidato,
            CancellationToken cancellationToken = default)
        {
            // Negación por defecto: no aceptar acceso anónimo ni claims incompletos.
            if (usuario?.Identity?.IsAuthenticated != true)
                return null;

            var userId = usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId) || userId.Length > 450)
                return null;

            // La entrada HTTP únicamente indica la empresa que solicita el
            // usuario; la pertenencia se valida siempre contra SQL Server.
            var slug = SlugEmpresaSeguro.Normalizar(slugCandidato);
            if (slug == null)
                return null;

            const string sql = @"
SELECT TOP (1) e.IdEmpresa, eu.Rol
FROM dbo.EmpresaUsuarios AS eu
INNER JOIN dbo.Empresas AS e ON e.IdEmpresa = eu.IdEmpresa
WHERE eu.UserId = @UserId
  AND e.SlugNormalizado = @Slug
  AND eu.Estado = N'Activo'
  AND eu.FechaBajaUtc IS NULL
  AND e.Estado = N'Activa';";

            await using var conexion = new SqlConnection(_connectionString);
            await conexion.OpenAsync(cancellationToken);

            await using var comando = new SqlCommand(sql, conexion);
            comando.Parameters.Add("@UserId", SqlDbType.NVarChar, 450).Value = userId;
            comando.Parameters.Add("@Slug", SqlDbType.NVarChar, 63).Value = slug!;

            await using var lector = await comando.ExecuteReaderAsync(
                CommandBehavior.SingleRow, cancellationToken);
            if (!await lector.ReadAsync(cancellationToken))
                return null;

            var rol = lector.GetString(1);
            // Aunque hay CHECK en SQL Server, negar permisos si un dato
            // inválido llegara por alguna importación o modificación futura.
            if (rol is not ("Propietario" or "Administrador" or "Cajero"))
                return null;

            return new MembresiaEmpresaValidada(lector.GetGuid(0), rol);
        }

    }
}

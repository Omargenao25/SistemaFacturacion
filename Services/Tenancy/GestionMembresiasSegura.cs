using System.Data;
using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

namespace SistemaFacturacion.Services.Tenancy
{
    /// <summary>
    /// Escrituras SQL transaccionales para seguridad SaaS.
    /// No usa ApplicationDbContext: evita depender de un filtro tenant para
    /// las tablas GLOBALES y permite auditoría atómica con cada cambio.
    /// NO envía correos; devuelve un token solamente al propietario al crearlo.
    /// </summary>
    public sealed class GestionMembresiasSegura
    {
        private readonly string _connectionString;
        private readonly ContextoEmpresaVerificado _empresa;

        public GestionMembresiasSegura(IConfiguration config, ContextoEmpresaVerificado empresa)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Falta DefaultConnection");
            _empresa = empresa;
        }

        public static bool RolDelegable(string? rol) =>
            rol is "Administrador" or "Cajero";

        public static string NormalizarCorreo(string correo)
        {
            var limpio = correo?.Trim();
            if (string.IsNullOrEmpty(limpio) || limpio.Length > 254 ||
                limpio.Any(char.IsControl) ||
                !MailAddress.TryCreate(limpio, out var direccion) ||
                !string.Equals(direccion.Address, limpio, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Introduce un correo electrónico válido.");
            return limpio.ToUpperInvariant();
        }

        private async Task<(SqlConnection, SqlTransaction)> AbrirAsync(CancellationToken ct)
        {
            var conexion = new SqlConnection(_connectionString);
            await conexion.OpenAsync(ct);
            try
            {
                var tx = (SqlTransaction)await conexion.BeginTransactionAsync(
                    IsolationLevel.Serializable, ct);
                return (conexion, tx);
            }
            catch
            {
                await conexion.DisposeAsync();
                throw;
            }
        }

        private static SqlCommand Comando(SqlConnection c, SqlTransaction tx, string sql)
            => new(sql, c, tx) { CommandTimeout = 20 };

        private static void Param(SqlCommand c, string n, SqlDbType tipo, object valor, int longitud = 0)
        {
            var p = longitud > 0
                ? c.Parameters.Add(n, tipo, longitud)
                : c.Parameters.Add(n, tipo);
            p.Value = valor;
        }

        private async Task ExigirPropietarioAsync(
            SqlConnection c, SqlTransaction tx, string actor, CancellationToken ct)
        {
            if (!_empresa.TieneEmpresa || _empresa.RolEmpresa != "Propietario" ||
                string.IsNullOrWhiteSpace(actor) || actor.Length > 450)
                throw new UnauthorizedAccessException("Operación sin permiso empresarial.");

            const string sql = @"
SELECT COUNT_BIG(*)
FROM dbo.EmpresaUsuarios eu WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.Empresas e WITH (UPDLOCK, HOLDLOCK)
    ON e.IdEmpresa = eu.IdEmpresa
WHERE eu.IdEmpresa = @Empresa AND eu.UserId = @Actor
  AND eu.Rol = N'Propietario'
  AND eu.Estado = N'Activo' AND eu.FechaBajaUtc IS NULL
  AND e.Estado = N'Activa';";
            await using var cmd = Comando(c, tx, sql);
            Param(cmd, "@Empresa", SqlDbType.UniqueIdentifier, _empresa.IdEmpresa);
            Param(cmd, "@Actor", SqlDbType.NVarChar, actor, 450);
            if (Convert.ToInt64(await cmd.ExecuteScalarAsync(ct)) != 1)
                throw new UnauthorizedAccessException("La membresía del propietario no está vigente.");
        }

        public async Task CambiarAsync(
            string actor, string afectado, byte[] rowVersion,
            string accion, string? nuevoRol, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(afectado) || afectado.Length > 450 ||
                afectado == actor || rowVersion is not { Length: 8 })
                throw new InvalidOperationException("Selecciona otra membresía válida y actualizada.");

            if (accion is not ("Rol" or "Suspender" or "Reactivar") ||
                (accion == "Rol" && !RolDelegable(nuevoRol)))
                throw new InvalidOperationException("La operación o el nuevo rol no están permitidos.");

            var (conexion, tx) = await AbrirAsync(ct);
            await using (conexion)
            await using (tx)
            {
                await ExigirPropietarioAsync(conexion, tx, actor, ct);
                string rolAnterior, estadoAnterior;
                byte[] versionReal;
                {
                    const string sql = @"
SELECT Rol, Estado, RowVersion
FROM dbo.EmpresaUsuarios WITH (UPDLOCK, HOLDLOCK)
WHERE IdEmpresa = @Empresa AND UserId = @Afectado
  AND FechaBajaUtc IS NULL;";
                    await using var cmd = Comando(conexion, tx, sql);
                    Param(cmd, "@Empresa", SqlDbType.UniqueIdentifier, _empresa.IdEmpresa);
                    Param(cmd, "@Afectado", SqlDbType.NVarChar, afectado, 450);
                    await using var lector = await cmd.ExecuteReaderAsync(ct);
                    if (!await lector.ReadAsync(ct))
                        throw new InvalidOperationException("Membresía no encontrada o dada de baja.");
                    rolAnterior = lector.GetString(0);
                    estadoAnterior = lector.GetString(1);
                    versionReal = (byte[])lector.GetValue(2);
                }

                // Proteger TODAS las cuentas Propietario, incluido el último.
                // Ni su rol ni estado pueden ser modificados desde la UI.
                if (rolAnterior == "Propietario")
                    throw new InvalidOperationException(
                        "Los propietarios no pueden modificarse desde este módulo.");

                if (!versionReal.SequenceEqual(rowVersion))
                    throw new InvalidOperationException(
                        "El empleado fue modificado por otra sesión. Actualiza la página.");

                var rolNuevo = accion == "Rol" ? nuevoRol! : rolAnterior;
                var estadoNuevo = accion switch
                {
                    "Suspender" => "Suspendido",
                    "Reactivar" => "Activo",
                    _ => estadoAnterior
                };
                if (estadoAnterior is not ("Activo" or "Suspendido") ||
                    (accion == "Rol" && estadoAnterior != "Activo") ||
                    (accion == "Suspender" && estadoAnterior != "Activo") ||
                    (accion == "Reactivar" && estadoAnterior != "Suspendido") ||
                    (rolNuevo == rolAnterior && estadoNuevo == estadoAnterior))
                    throw new InvalidOperationException(
                        "No se puede realizar esa transición con el estado actual.");

                {
                    const string sql = @"
UPDATE dbo.EmpresaUsuarios SET Rol = @Rol, Estado = @Estado
WHERE IdEmpresa = @Empresa AND UserId = @Afectado
  AND Rol <> N'Propietario' AND RowVersion = @Version
  AND FechaBajaUtc IS NULL;";
                    await using var cmd = Comando(conexion, tx, sql);
                    Param(cmd, "@Rol", SqlDbType.NVarChar, rolNuevo, 30);
                    Param(cmd, "@Estado", SqlDbType.NVarChar, estadoNuevo, 20);
                    Param(cmd, "@Empresa", SqlDbType.UniqueIdentifier, _empresa.IdEmpresa);
                    Param(cmd, "@Afectado", SqlDbType.NVarChar, afectado, 450);
                    Param(cmd, "@Version", SqlDbType.Binary, rowVersion, 8);
                    if (await cmd.ExecuteNonQueryAsync(ct) != 1)
                        throw new InvalidOperationException("Cambio concurrente; recarga el equipo.");
                }

                var nombreAccion = accion switch
                {
                    "Suspender" => "Suspendido",
                    "Reactivar" => "Reactivado",
                    _ => "RolCambiado"
                };
                await AuditarAsync(conexion, tx, actor, afectado, null,
                    nombreAccion, rolAnterior, rolNuevo, estadoAnterior, estadoNuevo, ct);
                await tx.CommitAsync(ct);
            }
        }

        public async Task<string> CrearInvitacionAsync(
            string actor, string correo, string rol, CancellationToken ct)
        {
            if (!RolDelegable(rol))
                throw new InvalidOperationException(
                    "Las invitaciones solo pueden asignar Administrador o Cajero.");
            var normalizado = NormalizarCorreo(correo);
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(tokenBytes)
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var hash = SHA256.HashData(tokenBytes);
            var id = Guid.NewGuid();
            var ahora = DateTime.UtcNow;

            var (conexion, tx) = await AbrirAsync(ct);
            await using (conexion)
            await using (tx)
            {
                await ExigirPropietarioAsync(conexion, tx, actor, ct);
                // No iniciar una segunda invitación si hay una pendiente.
                // Una invitación caducada se marca antes del índice único.
                {
                    const string sql = @"
UPDATE dbo.InvitacionesEmpresa SET Estado = N'Caducada'
WHERE IdEmpresa = @Empresa AND EmailNormalizado = @Email
 AND Estado = N'Pendiente' AND VenceUtc <= SYSUTCDATETIME();";
                    await using var cmd = Comando(conexion, tx, sql);
                    Param(cmd, "@Empresa", SqlDbType.UniqueIdentifier, _empresa.IdEmpresa);
                    Param(cmd, "@Email", SqlDbType.NVarChar, normalizado, 256);
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                {
                    // Evitar invitaciones para una cuenta que ya pertenezca
                    // a la empresa (aunque su membresía esté suspendida).
                    const string sql = @"
SELECT COUNT_BIG(*) FROM dbo.EmpresaUsuarios eu WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.AspNetUsers u ON u.Id = eu.UserId
WHERE eu.IdEmpresa = @Empresa AND u.NormalizedEmail = @Email;";
                    await using var cmd = Comando(conexion, tx, sql);
                    Param(cmd, "@Empresa", SqlDbType.UniqueIdentifier, _empresa.IdEmpresa);
                    Param(cmd, "@Email", SqlDbType.NVarChar, normalizado, 256);
                    if (Convert.ToInt64(await cmd.ExecuteScalarAsync(ct)) != 0)
                        throw new InvalidOperationException(
                            "Esa cuenta ya tiene una membresía en esta empresa.");
                }
                {
                    const string sql = @"
INSERT INTO dbo.InvitacionesEmpresa
 (IdInvitacion, IdEmpresa, EmailNormalizado, Rol, TokenHash,
  Estado, CreadoPorUserId, CreadoUtc, VenceUtc)
VALUES (@Id, @Empresa, @Email, @Rol, @Hash, N'Pendiente',
        @Actor, @Creado, @Vence);";
                    await using var cmd = Comando(conexion, tx, sql);
                    Param(cmd, "@Id", SqlDbType.UniqueIdentifier, id);
                    Param(cmd, "@Empresa", SqlDbType.UniqueIdentifier, _empresa.IdEmpresa);
                    Param(cmd, "@Email", SqlDbType.NVarChar, normalizado, 256);
                    Param(cmd, "@Rol", SqlDbType.NVarChar, rol, 30);
                    Param(cmd, "@Hash", SqlDbType.VarBinary, hash, 32);
                    Param(cmd, "@Actor", SqlDbType.NVarChar, actor, 450);
                    Param(cmd, "@Creado", SqlDbType.DateTime2, ahora);
                    Param(cmd, "@Vence", SqlDbType.DateTime2, ahora.AddDays(2));
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                await AuditarAsync(conexion, tx, actor, null, id,
                    "InvitacionCreada", null, rol, null, "Pendiente", ct);
                await tx.CommitAsync(ct);
            }
            // El token NUNCA se guarda sin hash. Solo se entrega una vez
            // al propietario para compartirlo manualmente por canal seguro.
            return token;
        }

        public async Task AceptarAsync(
            string usuario, string emailNormalizado, bool correoConfirmado,
            string token, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(usuario) || usuario.Length > 450 ||
                !correoConfirmado || string.IsNullOrWhiteSpace(emailNormalizado) ||
                string.IsNullOrWhiteSpace(token) || token.Length > 128)
                throw new InvalidOperationException("Inicia sesión con el correo confirmado al que se invitó.");

            byte[] bytes;
            try
            {
                var b64 = token.Replace('-', '+').Replace('_', '/');
                b64 = b64.PadRight((b64.Length + 3) / 4 * 4, '=');
                bytes = Convert.FromBase64String(b64);
                if (bytes.Length != 32)
                    throw new FormatException();
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("Enlace de invitación inválido.");
            }

            var hash = SHA256.HashData(bytes);
            var (conexion, tx) = await AbrirAsync(ct);
            await using (conexion)
            await using (tx)
            {
                Guid id, empresaId;
                string destinatario, rol;
                DateTime vencimiento;
                string estado;
                {
                    const string sql = @"
SELECT i.IdInvitacion, i.IdEmpresa, i.EmailNormalizado, i.Rol,
       i.VenceUtc, i.Estado
FROM dbo.InvitacionesEmpresa i WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.Empresas e WITH (HOLDLOCK)
 ON e.IdEmpresa = i.IdEmpresa AND e.Estado = N'Activa'
WHERE i.TokenHash = @Hash;";
                    await using var cmd = Comando(conexion, tx, sql);
                    Param(cmd, "@Hash", SqlDbType.VarBinary, hash, 32);
                    await using var rd = await cmd.ExecuteReaderAsync(ct);
                    if (!await rd.ReadAsync(ct))
                        throw new InvalidOperationException("La invitación no está disponible.");
                    id = rd.GetGuid(0);
                    empresaId = rd.GetGuid(1);
                    destinatario = rd.GetString(2);
                    rol = rd.GetString(3);
                    vencimiento = rd.GetDateTime(4);
                    estado = rd.GetString(5);
                }

                if (estado != "Pendiente" || vencimiento <= DateTime.UtcNow ||
                    !RolDelegable(rol) ||
                    !string.Equals(destinatario, emailNormalizado, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Invitación caducada, consumida o destinada a otro correo.");

                // Revalidar correo confirmado DENTRO de la transacción,
                // independientemente del estado de la cookie Identity.
                {
                    const string sql = @"
SELECT COUNT_BIG(*) FROM dbo.AspNetUsers WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @Usuario AND NormalizedEmail = @Email AND EmailConfirmed = 1;";
                    await using var cmd = Comando(conexion, tx, sql);
                    Param(cmd, "@Usuario", SqlDbType.NVarChar, usuario, 450);
                    Param(cmd, "@Email", SqlDbType.NVarChar, destinatario, 256);
                    if (Convert.ToInt64(await cmd.ExecuteScalarAsync(ct)) != 1)
                        throw new InvalidOperationException("Debes confirmar el correo de la invitación.");
                }
                {
                    const string sql = @"
SELECT COUNT_BIG(*) FROM dbo.EmpresaUsuarios WITH (UPDLOCK, HOLDLOCK)
WHERE IdEmpresa = @Empresa AND UserId = @Usuario;";
                    await using var cmd = Comando(conexion, tx, sql);
                    Param(cmd, "@Empresa", SqlDbType.UniqueIdentifier, empresaId);
                    Param(cmd, "@Usuario", SqlDbType.NVarChar, usuario, 450);
                    if (Convert.ToInt64(await cmd.ExecuteScalarAsync(ct)) != 0)
                        throw new InvalidOperationException(
                            "Tu cuenta ya está asociada a esta empresa.");
                }
                {
                    const string sql = @"
INSERT INTO dbo.EmpresaUsuarios
 (IdEmpresa, UserId, Rol, Estado, FechaAltaUtc, FechaBajaUtc)
VALUES (@Empresa, @Usuario, @Rol, N'Activo', SYSUTCDATETIME(), NULL);

UPDATE dbo.InvitacionesEmpresa
SET Estado = N'Aceptada', ConsumidoUtc = SYSUTCDATETIME(),
    ConsumidoPorUserId = @Usuario
WHERE IdInvitacion = @Id AND Estado = N'Pendiente'
  AND VenceUtc > SYSUTCDATETIME();";
                    await using var cmd = Comando(conexion, tx, sql);
                    Param(cmd, "@Empresa", SqlDbType.UniqueIdentifier, empresaId);
                    Param(cmd, "@Usuario", SqlDbType.NVarChar, usuario, 450);
                    Param(cmd, "@Rol", SqlDbType.NVarChar, rol, 30);
                    Param(cmd, "@Id", SqlDbType.UniqueIdentifier, id);
                    if (await cmd.ExecuteNonQueryAsync(ct) != 2)
                        throw new InvalidOperationException("No pudo completarse la invitación.");
                }

                await AuditarAsync(conexion, tx, usuario, usuario, id,
                    "InvitacionAceptada", null, rol, "Pendiente", "Activo", ct,
                    empresaId);
                await tx.CommitAsync(ct);
            }
        }

        private async Task AuditarAsync(
            SqlConnection c, SqlTransaction tx,
            string actor, string? afectado, Guid? invitacion,
            string accion, string? rolAntes, string? rolDespues,
            string? estadoAntes, string? estadoDespues, CancellationToken ct,
            Guid? empresaSobrescrita = null)
        {
            const string sql = @"
INSERT INTO dbo.AuditoriaMembresias
 (IdEmpresa, ActorUserId, AfectadoUserId, IdInvitacion, Accion,
  RolAnterior, RolNuevo, EstadoAnterior, EstadoNuevo)
VALUES (@Empresa, @Actor, @Afectado, @Invitacion, @Accion,
        @RolAntes, @RolDespues, @EstadoAntes, @EstadoDespues);";
            await using var cmd = Comando(c, tx, sql);
            Param(cmd, "@Empresa", SqlDbType.UniqueIdentifier,
                empresaSobrescrita ?? _empresa.IdEmpresa);
            Param(cmd, "@Actor", SqlDbType.NVarChar, actor, 450);
            Param(cmd, "@Afectado", SqlDbType.NVarChar,
                (object?)afectado ?? DBNull.Value, 450);
            Param(cmd, "@Invitacion", SqlDbType.UniqueIdentifier,
                (object?)invitacion ?? DBNull.Value);
            Param(cmd, "@Accion", SqlDbType.NVarChar, accion, 40);
            Param(cmd, "@RolAntes", SqlDbType.NVarChar,
                (object?)rolAntes ?? DBNull.Value, 30);
            Param(cmd, "@RolDespues", SqlDbType.NVarChar,
                (object?)rolDespues ?? DBNull.Value, 30);
            Param(cmd, "@EstadoAntes", SqlDbType.NVarChar,
                (object?)estadoAntes ?? DBNull.Value, 20);
            Param(cmd, "@EstadoDespues", SqlDbType.NVarChar,
                (object?)estadoDespues ?? DBNull.Value, 20);
            if (await cmd.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("La auditoría no pudo registrarse.");
        }
    }
}

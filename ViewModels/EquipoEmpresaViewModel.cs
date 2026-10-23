namespace SistemaFacturacion.ViewModels
{
    /// <summary>
    /// Vista administrativa de SOLO LECTURA de una empresa validada.
    /// No transporta PasswordHash, SecurityStamp, RowVersion ni tokens.
    /// </summary>
    public sealed class EquipoEmpresaViewModel
    {
        public string Empresa { get; set; } = string.Empty;
        public string FiltroRol { get; set; } = "";
        public string FiltroEstado { get; set; } = "";
        public int Pagina { get; set; } = 1;
        public int TotalPaginas { get; set; } = 1;
        public int TotalMiembros { get; set; }
        public int Activos { get; set; }
        public int PropietariosActivos { get; set; }
        public int Invitados { get; set; }
        public int Suspendidos { get; set; }
        public List<MiembroEmpresaItem> Miembros { get; set; } = new();
        public List<PermisoRolItem> Matriz { get; set; } = new();
    }

    public sealed class MiembroEmpresaItem
    {
        // Identificador y RowVersion usados exclusivamente en forms POST con CSRF.
        // No son credenciales: el servidor revalida empresa, actor y target.
        public string UserId { get; set; } = string.Empty;
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public string NombreUsuario { get; set; } = "Usuario no disponible";
        public string Correo { get; set; } = "";
        public bool CorreoConfirmado { get; set; }
        public string Rol { get; set; } = "";
        public string Estado { get; set; } = "";
        public DateTime FechaAltaUtc { get; set; }
        public DateTime? FechaBajaUtc { get; set; }
        public bool Vigente => Estado == "Activo" && FechaBajaUtc == null;
        public string ClaseEstado => Vigente ? "good" :
            Estado == "Invitado" ? "warn" : "danger";
        public string EstadoVisible => FechaBajaUtc != null
            ? "Dada de baja"
            : Estado;
    }

    public sealed class PermisoRolItem
    {
        public string Nombre { get; set; } = "";
        public string Permiso { get; set; } = "";
        public bool Propietario { get; set; }
        public bool Administrador { get; set; }
        public bool Cajero { get; set; }
    }
}

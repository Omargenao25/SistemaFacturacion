namespace SistemaFacturacion.ViewModels
{
    /// <summary>
    /// Directorio administrativo. Nunca incluye token ni hash de invitación.
    /// Todos los campos proceden de una única empresa verificada en SQL.
    /// </summary>
    public sealed class InvitacionesEmpresaViewModel
    {
        public string Empresa { get; set; } = "";
        public string EstadoSeleccionado { get; set; } = "Pendiente";
        public int Pagina { get; set; } = 1;
        public int TotalPaginas { get; set; } = 1;
        public int TotalResultados { get; set; }
        public List<InvitacionEmpresaItem> Invitaciones { get; set; } = new();
    }

    public sealed class InvitacionEmpresaItem
    {
        public Guid IdInvitacion { get; set; }
        public string Correo { get; set; } = "";
        public string Rol { get; set; } = "";
        public string EstadoRegistrado { get; set; } = "";
        public DateTime CreadoUtc { get; set; }
        public DateTime VenceUtc { get; set; }
        public DateTime? ConsumidoUtc { get; set; }
        public string CreadoPor { get; set; } = "";

        public string EstadoVisible => EstadoRegistrado == "Pendiente" &&
            VenceUtc <= DateTime.UtcNow ? "Vencida" : EstadoRegistrado;

        public bool PuedeRevocar =>
            EstadoRegistrado == "Pendiente" && VenceUtc > DateTime.UtcNow;
    }

    public sealed class PaginaInvitacionesEmpresa
    {
        public int Pagina { get; set; } = 1;
        public int Total { get; set; }
        public List<InvitacionEmpresaItem> Registros { get; set; } = new();
    }
}

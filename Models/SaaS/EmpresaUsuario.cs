using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace SistemaFacturacion.Models.SaaS
{
    // La autorización se debe verificar por EmpresaId + UserId en el servidor;
    // crear esta fila no concede aún permisos sobre datos comerciales.
    public class EmpresaUsuario
    {
        public Guid IdEmpresa { get; set; }

        public Empresa? Empresa { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public IdentityUser? Usuario { get; set; }

        [Required]
        [StringLength(30)]
        public string Rol { get; set; } = "Cajero";

        // Estado seguro por defecto: ninguna membresía se activa por accidente.
        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "Suspendido";

        public DateTime FechaAltaUtc { get; set; } = DateTime.UtcNow;
        public DateTime? FechaBajaUtc { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}

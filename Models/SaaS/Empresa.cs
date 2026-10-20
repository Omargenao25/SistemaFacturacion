using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacion.Models.SaaS
{
    // Registro de la organización en la plataforma; NO habilita por sí solo
    // acceso a registros comerciales ni crea un tenant activo.
    public class Empresa
    {
        [Key]
        public Guid IdEmpresa { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(63, MinimumLength = 3)]
        public string SlugNormalizado { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string NombreComercial { get; set; } = string.Empty;

        [StringLength(30)]
        public string? Rnc { get; set; }

        [Required]
        [StringLength(30)]
        public string Estado { get; set; } = "PendienteConfiguracion";

        [Required]
        [StringLength(64)]
        public string ZonaHoraria { get; set; } = "America/Santo_Domingo";

        [Required]
        [StringLength(3)]
        public string Moneda { get; set; } = "DOP";

        public DateTime FechaCreacionUtc { get; set; } = DateTime.UtcNow;

        // Impide que ediciones administrativas simultáneas se pisen.
        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}

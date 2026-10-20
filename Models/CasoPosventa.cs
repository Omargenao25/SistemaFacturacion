using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacion.Models
{
    // Solicitud de devolución o reclamación de garantía.
    // No representa automáticamente un reembolso ni un movimiento de stock.
    public class CasoPosventa
    {
        // Fase 1B-1: asociado a la empresa inicial; el aislamiento de
        // solicitudes y permisos se implementará después de validar la migración.
        // Mientras exista un valor por defecto, NO activar múltiples empresas.
        public Guid IdEmpresa { get; set; }

        [Key]
        public int IdCaso { get; set; }

        [ForeignKey(nameof(DetalleFactura))]
        public int IdDetalle { get; set; }

        public DetalleFactura? DetalleFactura { get; set; }

        [Required]
        [StringLength(20)]
        public string Tipo { get; set; } = "Garantia";

        [Range(1, int.MaxValue)]
        public int Cantidad { get; set; }

        [Required]
        [StringLength(800, MinimumLength = 10)]
        public string Motivo { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "Abierto";

        public DateTime FechaSolicitud { get; set; } = DateTime.Now;
        public DateTime? FechaActualizacion { get; set; }

        [StringLength(1000)]
        public string? Resolucion { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}

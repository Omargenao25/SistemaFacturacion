using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacion.Models
{
    /// <summary>
    /// Solicitud contable generada al anular una factura.
    /// Nunca representa por sí misma dinero entregado al cliente.
    /// La confirmación requiere permisos verificados y un flujo posterior.
    /// </summary>
    public class Reembolso : IEntidadEmpresa
    {
        public Guid IdEmpresa { get; set; }

        [Key]
        public int IdReembolso { get; set; }

        public int IdFactura { get; set; }
        public Factura? Factura { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99")]
        public decimal MontoSolicitado { get; set; }

        // Método que se utilizó para cobrar, NO necesariamente para devolver.
        [Required, StringLength(30)]
        public string MetodoPagoOriginal { get; set; } = string.Empty;

        // Pendiente y PendienteRevision no indican una salida de fondos.
        [Required, StringLength(25)]
        public string Estado { get; set; } = "Pendiente";

        public DateTime FechaSolicitudUtc { get; set; } = DateTime.UtcNow;

        // Reservados para un proceso de devolución real con roles y auditoría.
        public DateTime? FechaConfirmacionUtc { get; set; }

        [StringLength(450)]
        public string? ConfirmadoPorUserId { get; set; }

        [StringLength(100)]
        public string? ReferenciaDevolucion { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}

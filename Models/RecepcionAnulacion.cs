using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacion.Models
{
    /// <summary>
    /// Obligación de recuperar y verificar las unidades de una línea
    /// tras ANULAR su factura. Pendiente NO significa recibida físicamente.
    /// No modifica existencias vendibles por sí sola.
    /// </summary>
    public class RecepcionAnulacion : IEntidadEmpresa
    {
        public Guid IdEmpresa { get; set; }

        [Key]
        public int IdRecepcion { get; set; }

        public int IdDetalle { get; set; }
        public DetalleFactura? DetalleFactura { get; set; }

        [Range(1, int.MaxValue)]
        public int CantidadEsperada { get; set; }

        // Campos reservados para inspección autorizada posterior.
        [Range(0, int.MaxValue)]
        public int CantidadRecibida { get; set; }

        [Range(0, int.MaxValue)]
        public int CantidadLiberada { get; set; }

        [Required, StringLength(24)]
        public string Estado { get; set; } = "Pendiente";

        public DateTime FechaCreacionUtc { get; set; } = DateTime.UtcNow;
        public DateTime? FechaRecepcionUtc { get; set; }

        [StringLength(450)]
        public string? RecibidoPorUserId { get; set; }

        [StringLength(1000)]
        public string? Observaciones { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}

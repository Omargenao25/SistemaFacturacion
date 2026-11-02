using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacion.Models;

/// <summary>
/// Borrador interno de una nota de credito. No es un comprobante fiscal emitido
/// ni una orden de devolucion de fondos. No habilitar escrituras sin servicio
/// transaccional, control de permisos y validacion fiscal.
/// </summary>
public sealed class NotaCredito : IEntidadEmpresa
{
    public Guid IdEmpresa { get; set; }

    [Key]
    public int IdNotaCredito { get; set; }

    public int IdFactura { get; set; }
    public Factura? Factura { get; set; }

    [Required, StringLength(80)]
    public string Motivo { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Estado { get; set; } = "Borrador";

    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Impuesto { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Descuento { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    public DateTime FechaCreacionUtc { get; set; } = DateTime.UtcNow;

    [StringLength(450)]
    public string? CreadoPorUserId { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<DetalleNotaCredito> Detalles { get; set; } = new List<DetalleNotaCredito>();
}

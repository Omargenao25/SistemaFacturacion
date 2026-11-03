using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacion.Models;

/// <summary>Detalle de borrador asociado a una linea original de venta.</summary>
public sealed class DetalleNotaCredito : IEntidadEmpresa
{
    public Guid IdEmpresa { get; set; }

    [Key]
    public int IdDetalleNotaCredito { get; set; }

    // Debe coincidir con la factura de la nota Y con la de la línea vendida.
    // Ambas claves foráneas compuestas comparten este IdFactura.
    public int IdFactura { get; set; }

    public int IdNotaCredito { get; set; }
    public NotaCredito? NotaCredito { get; set; }

    public int IdDetalleFactura { get; set; }
    public DetalleFactura? DetalleFactura { get; set; }

    [Range(1, int.MaxValue)]
    public int Cantidad { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Impuesto { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Descuento { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }
}

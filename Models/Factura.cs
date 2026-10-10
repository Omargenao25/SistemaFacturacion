using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacion.Models
{
    public class Factura
    {
        [Key]
        public int IdFactura { get; set; }

        [Required]
        [StringLength(30)]
        public string NumeroFactura { get; set; } = string.Empty;

        [Required]
        [ForeignKey(nameof(Cliente))]
        public int IdCliente { get; set; }

        public Cliente? Cliente { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Impuesto { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Descuento { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        [StringLength(20)]
        public string Estado { get; set; } = "Emitida";

        public ICollection<DetalleFactura> Detalles { get; set; }
            = new List<DetalleFactura>();
    }
}
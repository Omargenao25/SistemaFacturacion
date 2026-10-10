using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacion.Models
{
    public class Pago
    {
        [Key]
        public int IdPago { get; set; }

        [ForeignKey(nameof(Factura))]
        public int IdFactura { get; set; }

        public Factura? Factura { get; set; }

        [Required]
        [StringLength(30)]
        public string MetodoPago { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Monto { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;
    }
}
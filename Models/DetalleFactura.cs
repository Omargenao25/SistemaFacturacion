using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacion.Models
{
    public class DetalleFactura
    {
        [Key]
        public int IdDetalle { get; set; }

        [ForeignKey(nameof(Factura))]
        public int IdFactura { get; set; }

        public Factura? Factura { get; set; }

        [ForeignKey(nameof(Producto))]
        public int IdProducto { get; set; }

        public Producto? Producto { get; set; }

        public int Cantidad { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Precio { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostoUnitario { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacion.Models
{
    public class DetalleFactura
    {
        // Fase 1B-1: asociado a la empresa inicial; el aislamiento de
        // solicitudes y permisos se implementará después de validar la migración.
        // Mientras exista un valor por defecto, NO activar múltiples empresas.
        public Guid IdEmpresa { get; set; }

        [Key]
        public int IdDetalle { get; set; }

        [ForeignKey(nameof(Factura))]
        public int IdFactura { get; set; }

        public Factura? Factura { get; set; }

        [ForeignKey(nameof(Producto))]
        public int IdProducto { get; set; }

        public Producto? Producto { get; set; }

        public int Cantidad { get; set; }

        // Conserva el plazo ofrecido al comprar, aunque el producto cambie.
        [Range(0, 120)]
        public int GarantiaMeses { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Precio { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostoUnitario { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }
    }
}
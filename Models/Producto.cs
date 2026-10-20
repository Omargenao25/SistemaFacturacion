using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacion.Models
{
    public class Producto
    {
        // Fase 1B-1: asociado a la empresa inicial; el aislamiento de
        // solicitudes y permisos se implementará después de validar la migración.
        // Mientras exista un valor por defecto, NO activar múltiples empresas.
        public Guid IdEmpresa { get; set; }

        [Key]
        public int IdProducto { get; set; }

        [Required]
        [StringLength(30)]
        public string Codigo { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Descripcion { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Precio { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Costo { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo.")]
        public int Stock { get; set; }

        // 0 indica que no se registró garantía comercial adicional.
        // El plazo se copia a cada detalle cuando se emite una factura.
        [Range(0, 120, ErrorMessage = "La garantía debe estar entre 0 y 120 meses.")]
        public int GarantiaMeses { get; set; }

        public bool Estado { get; set; } = true;

        // SQL Server actualiza automáticamente esta versión cada vez que
        // cambia la fila (ventas, anulaciones y ediciones incluidas).
        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        [Required]
        [ForeignKey(nameof(Categoria))]
        public int IdCategoria { get; set; }

        public Categoria? Categoria { get; set; }
    }
}
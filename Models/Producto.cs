using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacion.Models
{
    public class Producto
    {
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
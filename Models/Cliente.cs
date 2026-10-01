using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacion.Models
{
    public class Cliente
    {
        [Key]
        public int IdCliente { get; set; }

        [Required]
        [StringLength(50)]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Apellido { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Telefono { get; set; }

        [EmailAddress]
        public string? Correo { get; set; }

        [StringLength(150)]
        public string? Direccion { get; set; }

        [StringLength(30)]
        public string? IdentificacionFiscal { get; set; }
    }
}
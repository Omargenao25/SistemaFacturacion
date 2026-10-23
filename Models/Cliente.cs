using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacion.Models
{
    public class Cliente : IEntidadEmpresa
    {
        // Fase 1B-1: asociado a la empresa inicial; el aislamiento de
        // solicitudes y permisos se implementará después de validar la migración.
        // Mientras exista un valor por defecto, NO activar múltiples empresas.
        public Guid IdEmpresa { get; set; }

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
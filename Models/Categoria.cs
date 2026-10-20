using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacion.Models
{
    public class Categoria
    {
        // Fase 1B-1: asociado a la empresa inicial; el aislamiento de
        // solicitudes y permisos se implementará después de validar la migración.
        // Mientras exista un valor por defecto, NO activar múltiples empresas.
        public Guid IdEmpresa { get; set; }

        [Key]
        public int IdCategoria { get; set; }

        [Required]
        [StringLength(60)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(150)]
        public string? Descripcion { get; set; }
    }
}
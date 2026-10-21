using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacion.Models
{
    public class Pago : IEntidadEmpresa
    {
        // Fase 1B-1: asociado a la empresa inicial; el aislamiento de
        // solicitudes y permisos se implementará después de validar la migración.
        // Mientras exista un valor por defecto, NO activar múltiples empresas.
        public Guid IdEmpresa { get; set; }

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
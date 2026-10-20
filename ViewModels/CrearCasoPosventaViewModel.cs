using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacion.ViewModels
{
    public class CrearCasoPosventaViewModel
    {
        [Range(1, int.MaxValue)]
        public int IdDetalle { get; set; }

        [Required(ErrorMessage = "Selecciona el tipo de solicitud.")]
        [RegularExpression("^(Devolucion|Garantia)$",
            ErrorMessage = "Selecciona un tipo válido.")]
        public string Tipo { get; set; } = "Garantia";

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor que cero.")]
        public int Cantidad { get; set; } = 1;

        [Required(ErrorMessage = "Describe lo ocurrido.")]
        [StringLength(800, MinimumLength = 10,
            ErrorMessage = "Describe el caso con entre 10 y 800 caracteres.")]
        public string Motivo { get; set; } = string.Empty;

        // Campos informativos obtenidos de la base, nunca usados para autorizar el POST.
        public string NumeroFactura { get; set; } = string.Empty;
        public string NombreProducto { get; set; } = string.Empty;
        public DateTime FechaVenta { get; set; }
        public int CantidadVendida { get; set; }
        public int GarantiaMeses { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacion.ViewModels
{
    public class CrearFacturaViewModel
    {
        [Required(ErrorMessage = "Seleccione un cliente")]
        public int IdCliente { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "El descuento no puede ser negativo")]
        public decimal Descuento { get; set; }

        [Required]
        public string MetodoPago { get; set; } = "Efectivo";

        public List<DetalleFacturaInput> Detalles { get; set; } = new();
    }

    public class DetalleFacturaInput
    {
        [Range(1, int.MaxValue)]
        public int IdProducto { get; set; }

        [Range(1, int.MaxValue)]
        public int Cantidad { get; set; } = 1;
    }
}
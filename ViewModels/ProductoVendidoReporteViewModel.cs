namespace SistemaFacturacion.ViewModels
{
    public class ProductoVendidoReporteViewModel
    {
        public int IdProducto { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public int CantidadVendida { get; set; }

        public decimal TotalVendido { get; set; }
    }
}
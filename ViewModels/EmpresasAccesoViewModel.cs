namespace SistemaFacturacion.ViewModels
{
    public sealed class EmpresasAccesoViewModel
    {
        public int Pagina { get; set; } = 1;
        public int TotalPaginas { get; set; } = 1;
        public int TotalEmpresas { get; set; }
        public List<EmpresaAccesoItem> Empresas { get; set; } = new();
    }

    public sealed class EmpresaAccesoItem
    {
        public string Nombre { get; set; } = "";
        public string Slug { get; set; } = "";
        public string Rol { get; set; } = "";
        public string Moneda { get; set; } = "DOP";
    }
}

namespace SistemaFacturacion.ViewModels
{
    /// <summary>
    /// Proyección de lectura: los montos son solicitudes registradas, no
    /// salidas de caja ni reembolsos efectivamente pagados.
    /// </summary>
    public sealed class ControlAnulacionesViewModel
    {
        public string Buscar { get; set; } = string.Empty;
        public int Pagina { get; set; } = 1;
        public int TotalPaginas { get; set; } = 1;
        public int TotalFacturas { get; set; }

        // Los indicadores se refieren EXCLUSIVAMENTE a la página cargada.
        public int FacturasConSolicitudAbierta { get; set; }
        public decimal MontoSolicitudesAbiertas { get; set; }
        public long UnidadesPorRecibirRegistradas { get; set; }
        public int FacturasConDatosPorConciliar { get; set; }

        public List<ControlAnulacionItem> Facturas { get; set; } = new();
    }

    public sealed class ControlAnulacionItem
    {
        public int IdFactura { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public decimal TotalFactura { get; set; }

        public bool TieneReembolso { get; set; }
        public string EstadoReembolso { get; set; } = "Sin registro";
        public decimal? MontoSolicitado { get; set; }
        public string? MetodoPagoOriginal { get; set; }

        public string EstadoMercancia { get; set; } = "Sin registro";
        public long? UnidadesPendientes { get; set; }
        public int RegistrosRecepcion { get; set; }
        public int LineasFactura { get; set; }

        public bool SolicitudFinancieraAbierta =>
            TieneReembolso &&
            EstadoReembolso != "Reembolsado" &&
            EstadoReembolso != "Rechazado";

        public bool NecesitaConciliacion =>
            !TieneReembolso ||
            EstadoReembolso == "PendienteRevision" ||
            EstadoReembolso == "Fallido" ||
            LineasFactura == 0 ||
            RegistrosRecepcion != LineasFactura;

        public string ClaseReembolso =>
            EstadoReembolso == "Reembolsado" ? "good" :
            EstadoReembolso == "Sin registro" ||
            EstadoReembolso == "PendienteRevision" ||
            EstadoReembolso == "Fallido" ? "danger" : "warn";

        public string ClaseMercancia =>
            EstadoMercancia == "Completada" ? "good" :
            EstadoMercancia == "Sin registro" ||
            EstadoMercancia == "Revisar registros" ? "danger" : "warn";
    }
}

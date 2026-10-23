using SistemaFacturacion.Models;
using SistemaFacturacion.Services.Inventario;

namespace SistemaFacturacion.ViewModels
{
    /// <summary>
    /// Datos exclusivos de la página de simulación. No se enlazan con una
    /// operación de guardado y ningún valor del formulario muta la BD.
    /// </summary>
    public sealed class InspeccionRecepcionViewModel
    {
        public RecepcionAnulacion Recepcion { get; set; } = null!;

        public int CantidadNueva { get; set; }
        public int CantidadApta { get; set; }
        public int CantidadCuarentena { get; set; }
        public int CantidadReparacion { get; set; }
        public int CantidadDescartada { get; set; }

        public ResultadoInspeccion? Simulacion { get; set; }
    }
}

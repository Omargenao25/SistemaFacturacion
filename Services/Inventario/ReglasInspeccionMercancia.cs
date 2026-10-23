using System;
using System.Collections.Generic;

namespace SistemaFacturacion.Services.Inventario
{
    /// <summary>
    /// Reglas puras para preparar una inspección física.
    /// No realiza escrituras, autorizaciones ni movimientos de inventario.
    /// Los valores "aptos" representan unidades PROPUESTAS, nunca liberadas.
    /// </summary>
    public static class ReglasInspeccionMercancia
    {
        public static ResultadoInspeccion Evaluar(
            int cantidadEsperada,
            int cantidadYaRecibida,
            int cantidadNueva,
            int cantidadApta,
            int cantidadCuarentena,
            int cantidadReparacion,
            int cantidadDescartada)
        {
            var errores = new List<string>();

            if (cantidadEsperada <= 0)
                errores.Add("La cantidad esperada debe ser mayor que cero.");

            if (cantidadYaRecibida < 0 || cantidadYaRecibida > cantidadEsperada)
                errores.Add("La cantidad previamente recibida es inconsistente.");

            if (cantidadNueva <= 0)
                errores.Add("Indica al menos una unidad para la inspección simulada.");

            if (cantidadApta < 0 || cantidadCuarentena < 0 ||
                cantidadReparacion < 0 || cantidadDescartada < 0)
                errores.Add("Las cantidades por clasificación no pueden ser negativas.");

            // long previene desbordamientos si llegan valores int extremos por URL.
            long totalClasificado = (long)cantidadApta + cantidadCuarentena +
                                   cantidadReparacion + cantidadDescartada;

            if (totalClasificado != cantidadNueva)
                errores.Add("La suma de aptas, cuarentena, reparación y descarte debe coincidir con las unidades nuevas.");

            long totalRecibido = (long)cantidadYaRecibida + cantidadNueva;
            if (totalRecibido > cantidadEsperada)
                errores.Add("No puedes proponer más unidades que las pendientes de recibir.");

            return new ResultadoInspeccion
            {
                EsValida = errores.Count == 0,
                Errores = errores,
                CantidadPropuestaApta = errores.Count == 0 ? cantidadApta : 0,
                CantidadPropuestaRetenida = errores.Count == 0
                    ? cantidadCuarentena + cantidadReparacion + cantidadDescartada : 0,
                CantidadPendienteTrasSimulacion = errores.Count == 0
                    ? (int)((long)cantidadEsperada - totalRecibido) : 0
            };
        }
    }

    public sealed class ResultadoInspeccion
    {
        public bool EsValida { get; init; }
        public IReadOnlyList<string> Errores { get; init; } = Array.Empty<string>();
        public int CantidadPropuestaApta { get; init; }
        public int CantidadPropuestaRetenida { get; init; }
        public int CantidadPendienteTrasSimulacion { get; init; }
    }
}

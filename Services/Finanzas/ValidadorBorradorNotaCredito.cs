using SistemaFacturacion.Models;

namespace SistemaFacturacion.Services.Finanzas;

/// <summary>
/// Valida referencias de un borrador ya cargado desde consultas del tenant.
/// Es defensa de dominio; la escritura futura también necesitará transacción
/// y comprobaciones SQL para evitar concurrencia y manipulación de datos.
/// </summary>
public static class ValidadorBorradorNotaCredito
{
    public static void ValidarReferencias(NotaCredito nota, IReadOnlyCollection<DetalleFactura> lineasOriginales)
    {
        ArgumentNullException.ThrowIfNull(nota);
        ArgumentNullException.ThrowIfNull(lineasOriginales);

        if (nota.IdEmpresa == Guid.Empty || nota.IdFactura <= 0)
            throw new InvalidOperationException("La nota no tiene empresa o factura válida.");

        if (nota.Detalles == null || nota.Detalles.Count == 0)
            throw new InvalidOperationException("La nota debe contener detalles.");

        if (nota.Detalles.Any(d => d == null))
            throw new InvalidOperationException("Hay detalles nulos.");

        var originales = lineasOriginales.ToDictionary(d => d.IdDetalle);
        var idsUsados = new HashSet<int>();

        foreach (var detalle in nota.Detalles)
        {
            if (detalle.IdEmpresa != nota.IdEmpresa)
                throw new InvalidOperationException("Un detalle pertenece a otra empresa.");
            if (detalle.IdNotaCredito != nota.IdNotaCredito)
                throw new InvalidOperationException("El detalle no corresponde a la nota.");
            if (!idsUsados.Add(detalle.IdDetalleFactura))
                throw new InvalidOperationException("La misma línea aparece más de una vez.");
            if (!originales.TryGetValue(detalle.IdDetalleFactura, out var original) ||
                original.IdFactura != nota.IdFactura || original.IdEmpresa != nota.IdEmpresa)
                throw new InvalidOperationException("La línea de venta no pertenece a la factura de la nota.");

            CalculadoraNotaCredito.ValidarCantidad(original.Cantidad, 0, detalle.Cantidad);
        }
    }
}

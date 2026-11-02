using SistemaFacturacion.Models;

namespace SistemaFacturacion.Services.Finanzas;

/// <summary>
/// Validaciones de límites acumulados de un borrador. El llamador debe obtener
/// datos del servidor en una transacción que serialice emisiones concurrentes.
/// No emite documentos, no modifica stock y no devuelve dinero.
/// </summary>
public static class ValidadorLimitesCredito
{
    public static void Validar(
        NotaCredito borrador,
        decimal totalFactura,
        IReadOnlyCollection<DetalleFactura> lineasFactura,
        IReadOnlyCollection<NotaCredito> notasPrevias)
    {
        ArgumentNullException.ThrowIfNull(borrador);
        ArgumentNullException.ThrowIfNull(lineasFactura);
        ArgumentNullException.ThrowIfNull(notasPrevias);

        if (borrador.Estado != "Borrador")
            throw new InvalidOperationException("Solo se pueden validar borradores.");

        ValidadorBorradorNotaCredito.ValidarReferencias(borrador, lineasFactura);

        if (notasPrevias.Any(n =>
            n.IdEmpresa != borrador.IdEmpresa ||
            n.IdFactura != borrador.IdFactura ||
            n.IdNotaCredito == borrador.IdNotaCredito))
            throw new InvalidOperationException("El historial de créditos no corresponde a la factura.");

        // No contar borradores: solo documentos confirmados, cuando exista
        // una fase de emisión. Actualmente el modelo no admite dicho estado.
        // Rechazar historiales ambiguos en vez de asumir un saldo disponible.
        if (notasPrevias.Any(n => n.Estado != "Emitida"))
            throw new InvalidOperationException("No se puede conciliar un historial no emitido.");

        foreach (var detalle in borrador.Detalles)
        {
            var acreditadas = notasPrevias
                .SelectMany(n => n.Detalles)
                .Where(d => d.IdDetalleFactura == detalle.IdDetalleFactura)
                .Sum(d => (long)d.Cantidad);
            var vendidas = lineasFactura.Single(d => d.IdDetalle == detalle.IdDetalleFactura).Cantidad;
            if (acreditadas > int.MaxValue)
                throw new InvalidOperationException("Cantidad acumulada fuera de rango.");

            CalculadoraNotaCredito.ValidarCantidad(vendidas, (int)acreditadas, detalle.Cantidad);
        }

        if (borrador.Total != CalculadoraNotaCredito.CalcularTotal(
                borrador.Subtotal, borrador.Impuesto, borrador.Descuento))
            throw new InvalidOperationException("El total de la nota no concuerda con sus importes.");

        var totalDetalles = borrador.Detalles.Sum(d =>
            CalculadoraNotaCredito.CalcularTotal(d.Subtotal, d.Impuesto, d.Descuento));
        if (totalDetalles != borrador.Total)
            throw new InvalidOperationException("El total de los detalles no concuerda con la cabecera.");

        var totalAcreditado = notasPrevias.Sum(n => n.Total);
        CalculadoraNotaCredito.ValidarSolicitud(totalFactura, totalAcreditado, borrador.Total);
    }
}

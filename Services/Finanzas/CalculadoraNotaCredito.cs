namespace SistemaFacturacion.Services.Finanzas;

/// <summary>
/// Cálculos deterministas para preparar borradores de notas de crédito.
/// NO emite comprobantes fiscales, NO modifica facturas, stock ni pagos.
/// </summary>
public static class CalculadoraNotaCredito
{
    public static decimal CalcularTotal(decimal subtotal, decimal impuesto, decimal descuento)
    {
        if (subtotal < 0 || impuesto < 0 || descuento < 0)
            throw new ArgumentOutOfRangeException(nameof(subtotal), "Los importes no pueden ser negativos.");

        var bruto = checked(subtotal + impuesto);
        if (descuento > bruto)
            throw new ArgumentException("El descuento supera el importe bruto.");

        return decimal.Round(bruto - descuento, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Comprueba saldo disponible tras créditos ya emitidos (sin aceptar saldo negativo).
    /// Los valores provienen del servidor, no del formulario del cliente.
    /// </summary>
    public static decimal SaldoMaximo(decimal totalFactura, decimal totalNotasEmitidas)
    {
        if (totalFactura < 0 || totalNotasEmitidas < 0)
            throw new ArgumentOutOfRangeException(nameof(totalFactura), "No se permiten importes negativos.");
        if (totalNotasEmitidas > totalFactura)
            throw new InvalidOperationException("Los créditos acumulados exceden el total de la factura.");
        return decimal.Round(totalFactura - totalNotasEmitidas, 2, MidpointRounding.AwayFromZero);
    }

    public static void ValidarSolicitud(decimal totalFactura, decimal totalNotasEmitidas, decimal importeSolicitado)
    {
        if (importeSolicitado <= 0)
            throw new ArgumentOutOfRangeException(nameof(importeSolicitado), "El importe debe ser mayor que cero.");

        var saldo = SaldoMaximo(totalFactura, totalNotasEmitidas);
        if (importeSolicitado > saldo)
            throw new InvalidOperationException("El importe supera el saldo disponible.");
    }

    public static void ValidarCantidad(int cantidadVendida, int cantidadPreviamenteAcreditada, int cantidadSolicitada)
    {
        if (cantidadVendida <= 0 || cantidadPreviamenteAcreditada < 0 || cantidadSolicitada <= 0)
            throw new ArgumentOutOfRangeException(nameof(cantidadSolicitada), "Las cantidades no son válidas.");
        if (cantidadPreviamenteAcreditada > cantidadVendida ||
            cantidadSolicitada > cantidadVendida - cantidadPreviamenteAcreditada)
            throw new InvalidOperationException("La devolución supera las unidades vendidas disponibles.");
    }
}

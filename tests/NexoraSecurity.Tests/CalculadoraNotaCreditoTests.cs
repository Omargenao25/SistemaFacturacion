using SistemaFacturacion.Services.Finanzas;
using Xunit;

namespace NexoraSecurity.Tests;

public sealed class CalculadoraNotaCreditoTests
{
    [Theory]
    [InlineData(100, 18, 0, 118)]
    [InlineData(100, 18, 10, 108)]
    [InlineData(0, 0, 0, 0)]
    public void TotalCalculado(decimal subtotal, decimal impuesto, decimal descuento, decimal esperado) =>
        Assert.Equal(esperado, CalculadoraNotaCredito.CalcularTotal(subtotal, impuesto, descuento));

    [Fact]
    public void NoAceptaDescuentoSuperiorAlImporte() =>
        Assert.Throws<ArgumentException>(() => CalculadoraNotaCredito.CalcularTotal(100, 18, 119));

    [Fact]
    public void NoAceptaImportesNegativos() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CalculadoraNotaCredito.CalcularTotal(-1, 0, 0));

    [Theory]
    [InlineData(100, 0, 100)]
    [InlineData(100, 25, 75)]
    [InlineData(100, 100, 0)]
    public void SaldoRestante(decimal total, decimal acreditado, decimal esperado) =>
        Assert.Equal(esperado, CalculadoraNotaCredito.SaldoMaximo(total, acreditado));

    [Fact]
    public void BloqueaCreditoAcumuladoMayorQueFactura() =>
        Assert.Throws<InvalidOperationException>(() => CalculadoraNotaCredito.ValidarSolicitud(100, 80, 21));

    [Fact]
    public void PermiteCreditoParcialDentroDelSaldo() =>
        CalculadoraNotaCredito.ValidarSolicitud(100, 80, 20);

    [Fact]
    public void BloqueaImporteCero() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CalculadoraNotaCredito.ValidarSolicitud(100, 0, 0));

    [Fact]
    public void BloqueaSaldoInconsistente() =>
        Assert.Throws<InvalidOperationException>(() => CalculadoraNotaCredito.SaldoMaximo(100, 120));

    [Fact]
    public void PermiteCantidadParcial() =>
        CalculadoraNotaCredito.ValidarCantidad(10, 3, 7);

    [Fact]
    public void BloqueaCantidadMayorQueRestante() =>
        Assert.Throws<InvalidOperationException>(() => CalculadoraNotaCredito.ValidarCantidad(10, 3, 8));
}

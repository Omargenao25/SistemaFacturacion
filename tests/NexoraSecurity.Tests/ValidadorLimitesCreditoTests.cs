using SistemaFacturacion.Models;
using SistemaFacturacion.Services.Finanzas;
using Xunit;

namespace NexoraSecurity.Tests;

public sealed class ValidadorLimitesCreditoTests
{
    private static readonly Guid Empresa = Guid.Parse("11111111-1111-4111-8111-111111111111");

    private static (NotaCredito nota, DetalleFactura[] originales) Escenario()
    {
        var nota = new NotaCredito
        {
            IdEmpresa = Empresa, IdNotaCredito = 1, IdFactura = 10,
            Motivo = "Devolucion parcial", Estado = "Borrador",
            Subtotal = 100, Impuesto = 18, Total = 118
        };
        nota.Detalles.Add(new DetalleNotaCredito
        {
            IdEmpresa = Empresa, IdNotaCredito = 1, IdDetalleFactura = 20,
            Cantidad = 1, Subtotal = 100, Impuesto = 18, Total = 118
        });
        return (nota, new[] { new DetalleFactura
        {
            IdEmpresa = Empresa, IdFactura = 10, IdDetalle = 20, Cantidad = 2
        } });
    }

    [Fact]
    public void AceptaBorradorConsistente()
    {
        var (nota, originales) = Escenario();
        ValidadorLimitesCredito.Validar(nota, 236, originales, Array.Empty<NotaCredito>());
    }

    [Fact]
    public void RechazaMontoSuperiorAlTotalDeFactura()
    {
        var (nota, originales) = Escenario();
        Assert.Throws<InvalidOperationException>(() =>
            ValidadorLimitesCredito.Validar(nota, 100, originales, Array.Empty<NotaCredito>()));
    }

    [Fact]
    public void RechazaTotalesAlterados()
    {
        var (nota, originales) = Escenario();
        nota.Total = 119;
        Assert.Throws<InvalidOperationException>(() =>
            ValidadorLimitesCredito.Validar(nota, 236, originales, Array.Empty<NotaCredito>()));
    }

    [Fact]
    public void RechazaDescuadreDeDetalles()
    {
        var (nota, originales) = Escenario();
        nota.Detalles.Single().Total = 117;
        Assert.Throws<InvalidOperationException>(() =>
            ValidadorLimitesCredito.Validar(nota, 236, originales, Array.Empty<NotaCredito>()));
    }

    [Fact]
    public void RechazaHistorialSinEmisionVerificada()
    {
        var (nota, originales) = Escenario();
        var pendiente = new NotaCredito
        {
            IdEmpresa = Empresa, IdNotaCredito = 2, IdFactura = 10,
            Estado = "Borrador", Total = 1
        };
        Assert.Throws<InvalidOperationException>(() =>
            ValidadorLimitesCredito.Validar(nota, 236, originales, new[] { pendiente }));
    }

    [Fact]
    public void RechazaLineaQueNoEsDeLaFactura()
    {
        var (nota, originales) = Escenario();
        originales[0].IdFactura = 99;
        Assert.Throws<InvalidOperationException>(() =>
            ValidadorLimitesCredito.Validar(nota, 236, originales, Array.Empty<NotaCredito>()));
    }
}

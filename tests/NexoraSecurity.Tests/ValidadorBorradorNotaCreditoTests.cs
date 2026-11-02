using SistemaFacturacion.Models;
using SistemaFacturacion.Services.Finanzas;
using Xunit;

namespace NexoraSecurity.Tests;

public sealed class ValidadorBorradorNotaCreditoTests
{
    private static readonly Guid A = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid B = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static NotaCredito Borrador(int idFactura = 10, Guid? empresa = null)
    {
        var nota = new NotaCredito { IdEmpresa = empresa ?? A, IdNotaCredito = 5, IdFactura = idFactura, Motivo = "Devolución" };
        nota.Detalles.Add(new DetalleNotaCredito { IdEmpresa = empresa ?? A, IdNotaCredito = 5, IdDetalleFactura = 20, Cantidad = 2 });
        return nota;
    }

    private static DetalleFactura Original(int idFactura = 10, Guid? empresa = null) =>
        new() { IdEmpresa = empresa ?? A, IdFactura = idFactura, IdDetalle = 20, Cantidad = 3 };

    [Fact]
    public void AceptaLineaDeLaMismaFacturaYEmpresa() =>
        ValidadorBorradorNotaCredito.ValidarReferencias(Borrador(), new[] { Original() });

    [Fact]
    public void RechazaLineaDeOtraFactura() =>
        Assert.Throws<InvalidOperationException>(() =>
            ValidadorBorradorNotaCredito.ValidarReferencias(Borrador(), new[] { Original(idFactura: 99) }));

    [Fact]
    public void RechazaLineaDeOtraEmpresa() =>
        Assert.Throws<InvalidOperationException>(() =>
            ValidadorBorradorNotaCredito.ValidarReferencias(Borrador(), new[] { Original(empresa: B) }));

    [Fact]
    public void RechazaDetalleDeclaradoEnOtraEmpresa()
    {
        var nota = Borrador();
        nota.Detalles.First().IdEmpresa = B;
        Assert.Throws<InvalidOperationException>(() =>
            ValidadorBorradorNotaCredito.ValidarReferencias(nota, new[] { Original() }));
    }

    [Fact]
    public void RechazaUnidadesSuperioresALaVenta()
    {
        var nota = Borrador();
        nota.Detalles.First().Cantidad = 4;
        Assert.Throws<InvalidOperationException>(() =>
            ValidadorBorradorNotaCredito.ValidarReferencias(nota, new[] { Original() }));
    }

    [Fact]
    public void RechazaLineasDuplicadas()
    {
        var nota = Borrador();
        nota.Detalles.Add(new DetalleNotaCredito { IdEmpresa = A, IdNotaCredito = 5, IdDetalleFactura = 20, Cantidad = 1 });
        Assert.Throws<InvalidOperationException>(() =>
            ValidadorBorradorNotaCredito.ValidarReferencias(nota, new[] { Original() }));
    }

    [Fact]
    public void RechazaNotaSinDetalles()
    {
        var nota = Borrador();
        nota.Detalles.Clear();
        Assert.Throws<InvalidOperationException>(() =>
            ValidadorBorradorNotaCredito.ValidarReferencias(nota, new[] { Original() }));
    }
}

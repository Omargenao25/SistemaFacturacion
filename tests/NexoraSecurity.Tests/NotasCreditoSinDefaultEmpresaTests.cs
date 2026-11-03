using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models;
using SistemaFacturacion.Services.Tenancy;
using Xunit;

namespace NexoraSecurity.Tests;

public sealed class NotasCreditoSinDefaultEmpresaTests
{
    private sealed class ContextoEmpresa : IContextoEmpresa
    {
        public Guid IdEmpresa => Guid.Parse("11111111-1111-4111-8111-111111111111");
        public bool TieneEmpresa => true;
    }

    [Theory]
    [InlineData(typeof(NotaCredito))]
    [InlineData(typeof(DetalleNotaCredito))]
    public void NotaCredito_NoTieneEmpresaPredeterminadaEnSql(Type tipoEntidad)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        using var db = new ApplicationDbContext(options, new ContextoEmpresa());
        var entidad = db.Model.FindEntityType(tipoEntidad)!;
        var idEmpresa = entidad.FindProperty(nameof(IEntidadEmpresa.IdEmpresa))!;

        // GetDefaultValue() devuelve Guid.Empty por convención CLR aunque
        // no exista una cláusula DEFAULT en SQL Server. Inspeccionar las
        // anotaciones explícitas del modelo, no el valor efectivo.
        Assert.Null(idEmpresa.FindAnnotation("Relational:DefaultValue"));
        Assert.Null(idEmpresa.FindAnnotation("Relational:DefaultValueSql"));
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never,
            idEmpresa.ValueGenerated);
        Assert.NotNull(entidad.GetQueryFilter());
    }
}

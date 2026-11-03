using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Models;

namespace SistemaFacturacion.Services.Finanzas;

/// <summary>
/// Comprueba el origen de una nota de credito contra registros que EF Core
/// permite ver en el tenant actual. Operacion de solo lectura; NO emite notas.
/// Para escribir, volver a validar dentro de la transaccion y asegurar los
/// limites acumulados con bloqueo/concurrencia en SQL Server.
/// </summary>
public sealed class ValidadorOrigenNotaCredito
{
    private readonly ApplicationDbContext _db;

    public ValidadorOrigenNotaCredito(ApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task ValidarAsync(NotaCredito borrador, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(borrador);

        if (borrador.IdEmpresa == Guid.Empty || borrador.IdFactura <= 0 ||
            borrador.Detalles == null || borrador.Detalles.Count == 0)
            throw new InvalidOperationException("La nota no tiene empresa, factura o detalles validos.");

        // La factura tiene que ser visible para la empresa autenticada.
        // No usar IgnoreQueryFilters ni aceptar IdEmpresa del navegador como autoridad.
        var existeFactura = await _db.Facturas.AsNoTracking()
            .AnyAsync(f => f.IdFactura == borrador.IdFactura &&
                           f.IdEmpresa == borrador.IdEmpresa, cancelacion);
        if (!existeFactura)
            throw new InvalidOperationException("La factura no es accesible para la empresa.");

        var ids = borrador.Detalles.Select(d => d.IdDetalleFactura).Distinct().ToArray();

        // La consulta NO trae detalles de otras facturas ni de otras empresas.
        var originales = await _db.DetallesFactura.AsNoTracking()
            .Where(d => d.IdFactura == borrador.IdFactura &&
                        d.IdEmpresa == borrador.IdEmpresa &&
                        ids.Contains(d.IdDetalle))
            .ToListAsync(cancelacion);

        ValidadorBorradorNotaCredito.ValidarReferencias(borrador, originales);
    }
}

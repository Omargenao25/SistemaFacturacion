# NEXORA — Fase 9.9: corrección segura de IdFactura

**Alcance:** migración LOCAL `20261103023918_Fase9IntegridadFacturaDetalle.cs`. Este archivo aún no está en GitHub y por eso **no se ha cambiado desde el repositorio**. La primera migración `20261103004333_Fase9NotasCreditoBorradores` ya está versionada: no editarla retrospectivamente.

## Situación
EF Core generó `AddColumn<int>(name: "IdFactura", table: "DetallesNotaCredito", nullable: false, defaultValue: 0)`. Ese valor puede asociar incorrectamente filas existentes. En el laboratorio actual `DetallesNotaCredito` aún no existe; otras bases podrían tener datos.

## Corrección del método Up
Sustituir **solo** el `AddColumn<int>` anterior por el siguiente bloque. Mantener el resto de `Up` (índices, claves únicas, FKs) en su orden generado.

```csharp
migrationBuilder.AddColumn<int>(
    name: "IdFactura",
    table: "DetallesNotaCredito",
    type: "int",
    nullable: true);

// Recuperar IdFactura desde la línea histórica del mismo tenant.
// Rechazar filas huérfanas o discrepancias con la cabecera.
migrationBuilder.Sql("""
    UPDATE d
       SET d.IdFactura = df.IdFactura
    FROM dbo.DetallesNotaCredito AS d
    INNER JOIN dbo.DetallesFactura AS df
      ON df.IdEmpresa = d.IdEmpresa
     AND df.IdDetalle = d.IdDetalleFactura;

    IF EXISTS (
        SELECT 1
        FROM dbo.DetallesNotaCredito AS d
        LEFT JOIN dbo.NotasCredito AS n
          ON n.IdEmpresa = d.IdEmpresa
         AND n.IdNotaCredito = d.IdNotaCredito
        WHERE d.IdFactura IS NULL
           OR n.IdNotaCredito IS NULL
           OR n.IdFactura <> d.IdFactura
    )
        THROW 51391, 'NEXORA: detalle de nota sin factura original valida o con factura discordante.', 1;
    """);

migrationBuilder.AlterColumn<int>(
    name: "IdFactura",
    table: "DetallesNotaCredito",
    type: "int",
    nullable: false,
    oldClrType: typeof(int),
    oldType: "int",
    oldNullable: true);
```

Las claves compuestas nuevas deben aplicarse **después** del backfill. No crear las FKs antes de convertir la columna a obligatoria.

## Verificaciones obligatorias
- `dotnet build` y `dotnet test tests/NexoraSecurity.Tests/NexoraSecurity.Tests.csproj`.
- Leer `Up` y `Down` completos. `Down` debe retirar FKs e índices antes de quitar la columna.
- Generar un script SQL exclusivo de la transición, sin ejecutarlo.
- Si el script contiene `DEFAULT (0)` para `DetallesNotaCredito.IdFactura`, detenerse.
- Antes de ejecutar en una base descartable, comprobar **todas** las migraciones anteriores y respaldo. La migración actual supone que las tablas base existen.
- Ensayar el caso con cero registros y con registros válidos e inválidos en una copia descartable. Una inconsistencia debe provocar rollback, no corregirse con valores inventados.
- No aplicar al laboratorio ni a producción hasta revisar dependencias, SQL Server y el modelo de emisión.

## Nota sobre integridad
Las FKs de la Fase 9.8 incluyen `(IdEmpresa, IdFactura)` tanto hacia cabecera como hacia venta original, lo que impide relaciones entre facturas distintas. Eso no sustituye las futuras validaciones transaccionales de totales, cantidades acumuladas, permisos o idempotencia.

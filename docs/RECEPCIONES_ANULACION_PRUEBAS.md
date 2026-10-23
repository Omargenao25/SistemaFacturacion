# Nexora — Recepciones físicas pendientes tras anular facturas

**Rama:** `feature/nexora-ui`. **Estado:** código preparado; aún sin compilación local ni migración SQL aplicada en la base de desarrollo del usuario.

## Decisión de producto

La anulación de una factura y la devolución **física** de los artículos son procesos distintos:

1. La operación de **anular** sigue comprobando el plazo de 7 días, que la factura esté Emitida y que no se haya anulado antes (UPDATE condicional dentro de una transacción SQL Server).
2. La anulación crea la solicitud de reembolso Pendiente / PendienteRevision según los pagos registrados y **una recepción Pendiente por cada línea de factura**. Factura, reembolso y recepciones se registran en la **misma transacción**.
3. **No se incrementa el stock disponible al anular.** Aunque la factura se haya anulado, la mercancía todavía podría estar con el cliente. Las unidades vendidas permanecen descontadas del inventario hasta la recepción y revisión futura.
4. El detalle de la factura anulada muestra todas las recepciones pendientes con unidades esperadas y recibidas. El modal previo informa que no se restituye stock y no se devuelve dinero automáticamente.
5. No existe aún una opción habilitada para marcar mercancía recibida, liberar stock ni confirmar devolución de dinero: esas acciones necesitan autorización por empresa y usuario (trabajo de Russell), inspección, auditoría y controles de concurrencia.
6. Facturas anuladas **antes** de activar este módulo no se modifican ni se generan recepciones retroactivas. El panel avisa que requieren conciliación histórica, en lugar de insinuar que la mercancía fue recibida nuevamente.

## Implementación

- `Models/RecepcionAnulacion.cs`: `IEntidadEmpresa`, detalle de factura, cantidad esperada, recibida (inicial 0), liberada (inicial 0), estado Pendiente, fecha UTC, campos reservados de auditoría y `RowVersion`.
- `Data/ApplicationDbContext.cs`: `DbSet<RecepcionAnulacion> RecepcionesAnulacion`, filtro global por `IdEmpresa`, escritura validada, FK compuesta al detalle, índice único `(IdEmpresa, IdDetalle)`, restricciones CHECK de estado/cantidades.
- `Data/Migrations/20261021203000_RegistrarRecepcionesPendientesAnulacion.cs`: agrega **solo** tabla `RecepcionesAnulacion`, claves e índices. **No modifica datos previos ni reescribe stocks históricos**. Archivo Designer y snapshot EF incluidos.
- `Controllers/FacturasController.Anular`: elimina suma automática de stock; inserta recepciones pendientes por línea y reembolso pendiente antes del mismo `CommitAsync`; mantiene validación de plazo, estado e idempotencia.
- `Controllers/FacturasController.Details`: carga recepciones del mismo tenant, asociadas a los detalles de la factura.
- `Views/Facturas/Details.cshtml`: panel azul «Recepción de productos» y modal corregido. Los reembolsos mantienen su panel financiero.
- `wwwroot/css/site.css`: estilos del panel, responsive y oculto al imprimir.

## Antes de tocar SQL

1. Trabajar **solo con una base de desarrollo, no producción**. Respaldar y verificar restauración.
2. Actualizar rama:

```powershell
git switch feature/nexora-ui
git pull origin feature/nexora-ui
```

3. Compilar **Ctrl + Shift + B**. Si hay errores, no aplicar migraciones.
4. Confirmar que `Get-Migration` muestra `CrearReembolsosPendientes` como aplicada.
5. Si compiló y tienes respaldo confirmado, ejecutar:

```powershell
Update-Database
Get-Migration
```

La última migración debe ser `RegistrarRecepcionesPendientesAnulacion` (aplicada).

## Pruebas funcionales imprescindibles

| Caso | Resultado esperado |
| --- | --- |
| Crear factura de prueba con 2 líneas de producto | Stock se descuenta al emitir |
| Abrir modal de Anular | Advierte que el stock **NO volverá** al inventario al anular |
| Cerrar modal sin confirmar | Nada cambia |
| Confirmar Anular | Estado Anulada + Reembolso Pendiente (o PendienteRevision) + 2 recepciones pendientes, en la misma transacción |
| Revisar Productos | Stock **permanece descontado**; si era la última unidad seguirá Agotado |
| Revisar panel «Recepción de productos» | 2 líneas, unidades esperadas iguales a las vendidas, unidades recibidas = 0, Estado Pendiente |
| Repetir POST de anulación | Sin 2.º reembolso ni 2.ª recepción; stock no cambia |
| Factura anulada ANTES de la migración | No debe inventarse recepción nueva ni alterarse stock histórico |
| Probar factura vencida (más de 7 días) | Bloqueada: sin cambio en stock ni creación de recepción |
| Ver el comprobante impreso | No aparecen los paneles administrativos del reembolso/recepción |
| Clientes/Inventario/Reportes/Posventa | Sin regresión de operaciones existentes |

Consulta SQL de **solo lectura** tras las pruebas:

```sql
SELECT ra.IdRecepcion, ra.IdEmpresa, ra.IdDetalle,
       f.NumeroFactura, p.Nombre AS Producto,
       ra.CantidadEsperada, ra.CantidadRecibida, ra.CantidadLiberada,
       ra.Estado, ra.FechaCreacionUtc
FROM dbo.RecepcionesAnulacion ra
JOIN dbo.DetallesFactura d
  ON ra.IdEmpresa = d.IdEmpresa AND ra.IdDetalle = d.IdDetalle
JOIN dbo.Facturas f
  ON f.IdEmpresa = d.IdEmpresa AND f.IdFactura = d.IdFactura
JOIN dbo.Productos p
  ON p.IdEmpresa = d.IdEmpresa AND p.IdProducto = d.IdProducto
ORDER BY ra.IdRecepcion DESC;

-- Debe devolver cero filas:
SELECT IdEmpresa, IdDetalle, COUNT(*) AS Veces
FROM dbo.RecepcionesAnulacion
GROUP BY IdEmpresa, IdDetalle
HAVING COUNT(*) > 1;
```

## Bloqueantes antes de operar con clientes reales

- [ ] Integrar Identity + membresías + autorización por empresa y operación (Russell).
- [ ] Implementar registro auditable de **recepción física** con responsable, fecha y condición.
- [ ] Clasificar mercancía: apta para venta, cuarentena, reparación o descartada; no toda devolución debe volver a disponible.
- [ ] Al liberar stock, hacer incremento atómico e idempotente por unidades autorizadas; evitar doble liberación y controlar recepción parcial.
- [ ] Diseñar pruebas de dos operadores concurrentes, devolución parcial y anulación duplicada.
- [ ] Desacoplar confirmación de reembolso bancario/caja y reembolso parcial, con evidencia, permisos y conciliación.
- [ ] Política de anulación de transacciones por cobros mixtos, vales, tarjetas y DGII/nota de crédito cuando aplique.
- [ ] Resolver pendientes de aislamiento SaaS (defaults legados, protección de rutas, tests A/B, .NET soportado).

**Advertencia:** en esta versión las anulaciones nuevas NO reponen stock. No usarla como flujo de devolución comercial completo; la mercancía queda pendiente de recepción hasta implementar el proceso autorizado. No activar otra empresa ni fusionar el PR en `master`.

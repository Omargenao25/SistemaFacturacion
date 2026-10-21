# Nexora — Reembolsos pendientes generados al anular

**Estado:** implementación inicial en `feature/nexora-ui`; **sin compilación ni ejecución SQL local por el asistente**. Requiere respaldo y pruebas antes de uso real.

## Regla de negocio acordada

Anular factura **no equivale** a pagar un reembolso ni a devolver físicamente el producto. En la fase actual:

- Anulación válida (7 días, estado Emitida): cambia a Anulada y restituye el stock según lógica histórica.
- En **la misma transacción SQL** se crea exactamente **una solicitud de reembolso** vinculada a la factura y a la misma empresa.
- `MontoSolicitado` proviene de **SUM(Pago.Monto)** de los pagos originales, no se inventa desde `Factura.Total`.
- `MetodoPagoOriginal` indica el medio de cobro (Efectivo, Tarjeta, Transferencia; Mixto cuando difieren).
- Si hubo pagos positivos y su suma coincide exactamente con el total y comparten un solo método, `Estado = Pendiente`.
- Si falta un pago, la suma no coincide o hubo métodos mixtos, `Estado = PendienteRevision`: **no presumir que el saldo puede devolverse automáticamente**.
- Si existiera un pago negativo histórico, la anulación falla y revierte la transacción para que se revise el registro anómalo.
- No se emiten transferencias, no se entrega efectivo, no se llama a la procesadora de tarjetas, no se crea una nota de crédito fiscal y el pago original **no se elimina ni se marca devuelto**.
- Los reembolsos de facturas ya anuladas **antes de instalar esta migración no se inventan ni se crean retroactivamente**. Aparecerá «Sin solicitud de reembolso registrada» y deberán conciliarse posteriormente.
- No hay botones ni endpoint para aprobar, devolver o marcar `Reembolsado`. La creación ocurre **solo desde la anulación existente**, actualmente en Development local; la administración financiera se habilitará después de permisos comprobados de Russell.

**Riesgo conocido a tratar en próximo módulo:** el código histórico añade stock inmediatamente al anular aunque no registre una recepción física. No lo cambiamos en esta entrega para no romper el flujo ya probado. **No publicar ni usar con clientes reales** hasta separar anulación, recepción física, movimientos auditados y reembolsos.

## Cambios

- `Models/Reembolso.cs`: `IEntidadEmpresa`, importe, medio original, estado, fecha UTC y campos reservados para confirmar con auditoría.
- `Data/ApplicationDbContext.cs`: `DbSet<Reembolso>`, filtro global por empresa, asignación/verificación de `IdEmpresa`, FK compuesta a `Facturas(IdEmpresa,IdFactura)`, índice único `(IdEmpresa,IdFactura)`, CHECK sobre monto no negativo y estados.
- `Data/Migrations/20261021193000_CrearReembolsosPendientes.cs`: nueva tabla `Reembolsos`; sin cambios destructivos sobre facturas, pagos ni productos.
- `Controllers/FacturasController.cs`: `Anular` registra solicitud en la misma transacción SQL, sin poner dinero de vuelta automáticamente. `Details` carga la solicitud visible para la empresa.
- `Views/Facturas/Details.cshtml`: nota de reembolso, estado, importe y método original para una factura anulada; al confirmar anulación advierte que crea solicitud pero no entrega dinero.
- `wwwroot/css/site.css`: panel de seguimiento financiero.

## Aplicación solo en desarrollo

1. Actualiza `feature/nexora-ui`: `git pull origin feature/nexora-ui`.
2. `Ctrl+Shift+B`: **si falla, detenerse** y enviar el error completo.
3. Realiza un **respaldo restaurable** de la base de desarrollo. Verifica que estés en el SQL Server esperado.
4. Abre Package Manager Console y ejecuta `Get-Migration`: la anterior `AislarRelacionesComerciales` debe estar aplicada.
5. Solo si compile/backup/servidor son correctos: `Update-Database`, después `Get-Migration`.
6. La nueva migración `CrearReembolsosPendientes` debe figurar aplicada.

## Casos de prueba mínimos

| Caso | Resultado esperado |
| --- | --- |
| Nueva factura con pago efectivo, anular | Stock se restituye una vez; reembolso `Pendiente`, método `Efectivo`, monto cobrado |
| Factura con pago tarjeta, anular | `Pendiente`, método `Tarjeta`; **no** ejecutar devolución bancaria |
| Factura con pago transferencia, anular | `Pendiente`, método `Transferencia`; **no** realizar transferencia |
| Intentar anular por segunda vez | Sin segunda solicitud ni segunda restitución |
| Cerrar modal «Mantener factura» | Sin anulación ni solicitud |
| Anular factura fuera de 7 días | Bloqueado por servidor; sin solicitud ni cambio de stock |
| Anular con pagos inconsistentes (BD de pruebas separada) | `PendienteRevision`; nunca `Reembolsado` automático |
| Detalle de factura anulada ANTES del módulo | Nota de conciliación, **sin solicitud ficticia** |
| Modificar estado del pago original | No ocurre durante esta operación |
| Entrar a reportes, posventa, garantías, crear factura | Regresión sin cambios |
| Intentos tenant A/B (BD de pruebas) | FK `(IdEmpresa, IdFactura)` y filtros bloquean cruces |

Consulta de comprobación (SQL de **solo lectura**):

```sql
SELECT r.IdReembolso, r.IdEmpresa, r.IdFactura, f.NumeroFactura,
       f.Estado AS EstadoFactura, r.Estado AS EstadoReembolso,
       r.MontoSolicitado, r.MetodoPagoOriginal, r.FechaSolicitudUtc
FROM dbo.Reembolsos r
JOIN dbo.Facturas f ON f.IdEmpresa = r.IdEmpresa AND f.IdFactura = r.IdFactura
ORDER BY r.IdReembolso DESC;

-- Debe devolver 0 filas:
SELECT IdEmpresa, IdFactura, COUNT(*) Solicitudes
FROM dbo.Reembolsos
GROUP BY IdEmpresa, IdFactura
HAVING COUNT(*) > 1;
```

## Bloqueantes para producto comercial

- [ ] Integrar Identity y roles **por empresa** de Russell: solo Administrador/Propietario con permiso explícito podrá confirmar devoluciones.
- [ ] Diseñar `MovimientoCaja`, `TransaccionReembolso` y evidencia de entrega/confirmación bancaria, con soporte de intentos fallidos, reembolso parcial y conciliación; manejar reintentos idempotentes.
- [ ] No confirmar dinero automáticamente ni sin comprobante/referencia; proteger contra reembolsos superiores a monto pagado.
- [ ] Decidir stock: recepción física, inspección, cuarentena o disponibilidad. Hoy se repone directamente por la anulación heredada.
- [ ] Resolver DGII/notas de crédito y documentos fiscales conforme al régimen aplicable antes de habilitarlos.
- [ ] Completar seguridad multiempresa: eliminar defaults de legado, proteger rutas, consultas externas y pruebas A/B.
- [ ] Actualizar framework soportado y contar con tests de integración, respaldos y monitoreo.

**No fusionar PR #1 ni activar dinero real.**

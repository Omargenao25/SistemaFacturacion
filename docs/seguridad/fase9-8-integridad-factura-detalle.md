# NEXORA Fase 9.8 — Integridad factura / detalle en SQL Server

## Estado de esta rama
Se añadió `DetalleNotaCredito.IdFactura`. Las relaciones compuestas ahora son:
- `DetallesNotaCredito (IdEmpresa, IdFactura, IdNotaCredito)` -> `NotasCredito (IdEmpresa, IdFactura, IdNotaCredito)`.
- `DetallesNotaCredito (IdEmpresa, IdFactura, IdDetalleFactura)` -> `DetallesFactura (IdEmpresa, IdFactura, IdDetalle)`.

Como ambas relaciones comparten `IdEmpresa` e `IdFactura` en la fila dependiente, SQL Server puede rechazar que una línea acreditada pertenezca a una factura diferente de la cabecera, incluso dentro de la misma empresa.

**No hay endpoints de emisión ni cambios de dinero o inventario.**

## Migración de esta fase: PENDIENTE
La migración `20261103004333_Fase9NotasCreditoBorradores` ya está versionada y representa el **modelo anterior**. No editarla en sitio ni aplicar el modelo nuevo contra el esquema viejo. Debe generarse una **migración adicional** después de descargar estos cambios.

La migración adicional podría:
- Incorporar `IdFactura` en `DetallesNotaCredito`.
- Añadir claves alternativas/únicas en `NotasCredito` y `DetallesFactura`.
- Reemplazar claves foráneas e índices de `DetallesNotaCredito`.

**Riesgo:** `DetallesFactura` ya existe y contiene información histórica. Agregar una clave alternativa podría alterar esa tabla y requiere comprobar previamente duplicados, locks y duración. Si la migración nueva exige valores por defecto para registros anteriores, se debe rechazar hasta definir un backfill seguro que derive `IdFactura` de la línea original.

## Cómo continuar sin modificar SQL
1. Asegurarse de que el árbol local esté limpio; cualquier script SQL local debe guardarse o eliminarse solo por decisión del propietario.
2. `git pull origin feature/nexora-security-phase8-auth-navigation`.
3. `dotnet test tests/NexoraSecurity.Tests/NexoraSecurity.Tests.csproj`.
4. Solo si pasan: `dotnet ef migrations add Fase9IntegridadFacturaDetalle --project SistemaFacturacion.csproj`.
5. Leer íntegros los archivos Up/Down, snapshot y generar script de revisión. **No ejecutar `database update`.**
6. Revisar específicamente operaciones sobre `DetallesFactura`, el nuevo campo `IdFactura`, claves alternativas y FKs.
7. Ensayar secuencia de migraciones **en base descartable** con copia y respaldo, nunca directamente en el laboratorio existente ni producción.

## Pendientes antes de producción
- Pruebas de restricciones **reales** de SQL Server: aceptación de factura+línea válidas y rechazo de factura+línea discordantes.
- Control de límites monetarios y cantidades acumuladas en transacción ante concurrencia, reintentos e idempotencia.
- Validación del estado fiscal y de documentos autorizados aplicables a República Dominicana.

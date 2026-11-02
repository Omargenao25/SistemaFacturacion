# NEXORA fase 9.6 — preparación de migración SQL Server

Estado: **procedimiento de revisión; ninguna migración se ha creado o aplicado desde este documento**.

## Requisitos previos
- Estar en rama `feature/nexora-security-phase8-auth-navigation`.
- Ejecutar `dotnet build` y `dotnet test tests/NexoraSecurity.Tests/NexoraSecurity.Tests.csproj`.
- No utilizar `dotnet ef database update` hasta contar con una copia desechable de la base de datos y revisar el SQL.
- El modelo de nota de crédito es solo para borradores internos. No emite NCF/e-CF ni mueve dinero.

## Paso 1: comprobar entorno de migraciones
```powershell
git status --short
dotnet ef migrations list --project SistemaFacturacion.csproj
```
No generar cambios si existen modificaciones locales inesperadas.

## Paso 2: generar migración en código, sin aplicarla
```powershell
dotnet ef migrations add Fase9NotasCreditoBorradores --project SistemaFacturacion.csproj --output-dir Data/Migrations
```
**Revisar antes de hacer commit**. Si el proyecto tiene directorio de migraciones diferente, confirmar la ubicación primero. Evitar añadir una segunda migración con otro nombre si ya existe.

## Paso 3: revisar artefactos
Inspeccionar todos los archivos creados y el `ModelSnapshot`. El método `Up` debe crear únicamente `NotasCredito` y `DetallesNotaCredito` (más índices, restricciones y claves auxiliares de esas entidades). Si altera tablas existentes inesperadamente, detenerse.

Confirmar:
- `IdEmpresa` obligatorio y filtro de consultas en el modelo EF Core.
- FKs compuestas desde `NotasCredito` a `Facturas`, y desde `DetallesNotaCredito` a `NotasCredito` y `DetallesFactura`.
- `DeleteBehavior.NoAction` para mantener historial.
- Importes como `decimal(18,2)`, cantidades positivas, `RowVersion` para concurrencia.
- Índices sobre identificadores de empresa y de factura.
- **Riesgo pendiente:** dos FKs del detalle no prueban por sí mismas que la línea acreditada pertenezca a la factura de la cabecera. El validador de dominio todavía no equivale a una garantía transaccional de SQL.

## Paso 4: generar script para inspección, NO ejecutar
```powershell
dotnet ef migrations script --idempotent --output fase9-notas-credito-review.sql
```
Revisar el script con cuidado. No publicarlo con secretos, conexiones de producción ni datos reales.

## Paso 5: verificación en base descartable
Solo tras revisión de código y respaldo confirmado, restaurar una copia desechable, aplicar allí la migración y comprobar claves, índices, rollback e integridad. No aplicar sobre `NexoraSecurityLab_Phase8` ni bases de producción en esta etapa.

## Condiciones para habilitar emisión
- Diseño de control transaccional contra solicitudes simultáneas y duplicados.
- Conciliación de importes y cantidades históricos.
- Validación de la legislación fiscal dominicana y definición de NCF/e-CF.
- Pruebas HTTP con roles/antiforgery y pruebas SQL Server reales.
- Aprobación del equipo.

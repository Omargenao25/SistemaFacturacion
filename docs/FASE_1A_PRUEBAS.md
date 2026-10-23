# Nexora SaaS — Validación de fase 1A: Empresas y membresías

**Estado:** implementación subida a `feature/nexora-ui` con migración aditiva, **pendiente de compilar y probar en SQL Server local**.

## Alcance de esta entrega

- Nuevas entidades `Models/SaaS/Empresa.cs` y `Models/SaaS/EmpresaUsuario.cs`.
- `ApplicationDbContext` registra nuevos `DbSet` y Fluent API.
- `20261021001000_CrearEmpresasYMembresias` crea únicamente `Empresas` y `EmpresaUsuarios`.
- `EmpresaUsuario` referencia la tabla **existente** `AspNetUsers`, sin cambiar esa tabla.
- Empresas almacenan slug normalizado único y datos básicos. Membresías usan PK `(IdEmpresa, UserId)`, FK con NO ACTION, controles DB de estados/roles, `RowVersion`.
- Estado inicial de una `Empresa`: `PendienteConfiguracion`; de una membresía: `Suspendido`. **No se habilita acceso SaaS por insertar registros**.
- **No** existen todavía: CRUD de empresas, alta pública, cambio de empresa activo, filtros `EmpresaId` en datos comerciales, autorización tenant-aware, servicios de onboarding ni suscripción. No exponer Nexora a clientes reales.

## Pasos locales (desarrollo, no producción)

1. Guardar respaldo de la base de datos de desarrollo y verificar dónde apunta la cadena de conexión.
2. En Terminal de Visual Studio, asegurarse de estar en `feature/nexora-ui`; ejecutar `git pull origin feature/nexora-ui`.
3. Compilar con Ctrl+Shift+B. Si falla la compilación, **no ejecutar** `Update-Database`; compartir errores completos.
4. Abrir *Package Manager Console* y verificar que el proyecto predeterminado sea `SistemaFacturacion`.
5. Ejecutar `Get-Migration` para revisar el historial. La migración nueva debe aparecer como `CrearEmpresasYMembresias` pendiente.
6. Con respaldo y base correctos, ejecutar `Update-Database`. EF debería aplicar solo migraciones pendientes, incluida `20261021001000_CrearEmpresasYMembresias`.
7. Ejecutar Nexora y hacer pruebas de regresión de Clientes, Productos, emisión de facturas, garantías y Posventa. Estas pantallas no deben cambiar con este sprint.

## Verificación de esquema en SQL Server (en la misma BD de desarrollo)

```sql
SELECT name
FROM sys.tables
WHERE name IN ('Empresas', 'EmpresaUsuarios')
ORDER BY name;

SELECT t.name AS Tabla, i.name AS Indice, i.is_unique AS EsUnico
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
WHERE t.name IN ('Empresas', 'EmpresaUsuarios')
  AND i.is_primary_key = 0
ORDER BY t.name, i.name;

SELECT fk.name AS Restriccion, OBJECT_NAME(fk.parent_object_id) AS Tabla,
       OBJECT_NAME(fk.referenced_object_id) AS TablaReferenciada
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.parent_object_id) = 'EmpresaUsuarios';

SELECT name, OBJECT_NAME(parent_object_id) AS Tabla
FROM sys.check_constraints
WHERE OBJECT_NAME(parent_object_id) IN ('Empresas', 'EmpresaUsuarios');

SELECT COUNT(*) AS EmpresasCreadas FROM dbo.Empresas;
SELECT COUNT(*) AS MembresiasCreadas FROM dbo.EmpresaUsuarios;
```

**Resultado inicial esperado:** dos tablas; índice único del slug; PK compuesta; referencias a `Empresas` y `AspNetUsers`; tres restricciones CHECK; **0 empresas y 0 membresías**, salvo que el equipo haya creado registros deliberadamente (NO poblar datos reales de forma improvisada).

## Seguridad y migración segura

- Un registro de empresa **no** debe crear una cuenta ni permisos de venta automáticamente.
- No se debe añadir una segunda empresa por UI ni habilitar rutas multiempresa todavía.
- No se debe asociar `EmpresaId` a facturas antiguas sin migración de backfill controlada.
- La próxima migración de los registros comerciales será escalonada y tendrá pruebas A/B para impedir referencias entre tenants.
- Revisar con Russell la estrategia de roles y cualquier cambio previsto al tipo de usuario de Identity antes de integrar los modelos.
- No fusionar PR #1 con `master` hasta verificación de compilación, esquema SQL, pruebas de regresión y coordinación.

## Registro de pruebas

| Prueba | Resultado | Fecha |
| --- | --- | --- |
| Compilación .NET | Pendiente | — |
| `Get-Migration` | Pendiente | — |
| `Update-Database` | Pendiente | — |
| Índice único y FK | Pendiente | — |
| Revisión de CHECK + rowversion | Pendiente | — |
| Regresión ventas y garantías | Pendiente | — |
| Regresión Posventa | Pendiente | — |
| Aprobación de Russell para integración Identity | Pendiente | — |

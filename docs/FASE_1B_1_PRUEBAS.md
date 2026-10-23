# NEXORA SaaS — Fase 1B-1: datos comerciales asociados a la empresa inicial

**Estado:** cambios de código preparados en `feature/nexora-ui`. **No ejecutados en la base local y no compilados aquí**.

> **ATENCIÓN:** Esto NO es todavía aislamiento multiempresa. Solo permite conservar y etiquetar correctamente los datos anteriores sin romper el flujo mononegocio. **No publicar ni habilitar el registro de otra empresa**. El aislamiento y los permisos llegarán en 1B-2/1B-3.

## Qué cambia

- Nuevos `Guid IdEmpresa` obligatorios en: `Clientes`, `Categorias`, `Productos`, `Facturas`, `DetallesFactura`, `Pagos` y `CasosPosventa`.
- EF Core configura una FK de cada tabla hacia `Empresas` con `NoAction`, e índice por `IdEmpresa`.
- Empresa temporal estable: `09d17ec1-956c-4c6e-85b3-317c7cce4e01`; slug `nexora-legado`, nombre **Espacio inicial de compatibilidad**, estado `PendienteConfiguracion`.
- La migración `20261021003000_PrepararDatosComercialesPorEmpresa` **inserta** esa empresa si no existía, **solo si ninguna otra empresa existe en la base**. Asigna los datos anteriores y establece un **default de compatibilidad temporal** para nuevas inserciones. Si la BD ya tiene otra empresa, la migración falla deliberadamente sin mezclar datos.
- No cambia las relaciones históricas de facturación/inventario; tampoco cambia las vistas ni los controladores.
- **Pendiente**: tenant context y membresía validada, filtro de consultas por empresa, FKs compuestas entre tablas comerciales, índices únicos por empresa, autorización en todos los endpoints y retirada del default temporal.
- **No hay membresías nuevas** ni un propietario activo generado por esta migración. Aún no existe control de acceso SaaS.

## Antes de aplicar

1. **No aplicar a producción.** Confirmar que es una base de desarrollo y que no está conectada a un servicio público.
2. Hacer un respaldo de SQL Server y verificar que realmente puede restaurarse.
3. En SQL Server comprobar si hay empresas creadas:

```sql
SELECT IdEmpresa, SlugNormalizado, Estado FROM dbo.Empresas;
SELECT 'Clientes' Tabla, COUNT(*) Registros FROM dbo.Clientes
UNION ALL SELECT 'Productos', COUNT(*) FROM dbo.Productos
UNION ALL SELECT 'Facturas', COUNT(*) FROM dbo.Facturas
UNION ALL SELECT 'DetallesFactura', COUNT(*) FROM dbo.DetallesFactura
UNION ALL SELECT 'Pagos', COUNT(*) FROM dbo.Pagos
UNION ALL SELECT 'CasosPosventa', COUNT(*) FROM dbo.CasosPosventa
UNION ALL SELECT 'Categorias', COUNT(*) FROM dbo.Categorias;
```

**Resultado esperado del SELECT Empresas:** sin empresas, o solamente el ID de compatibilidad indicado arriba. Si hay otra empresa, **parar y revisar manualmente**.

## Ejecutar migración desde Visual Studio

```powershell
git switch feature/nexora-ui
git pull origin feature/nexora-ui
```

- Compilar `Ctrl+Shift+B` y resolver cualquier error primero.
- Comprobar la conexión activa a la **base de desarrollo respaldada**.
- En Package Manager Console:

```powershell
Get-Migration
Update-Database
Get-Migration
```

La última migración debe aparecer `PrepararDatosComercialesPorEmpresa` con `applied = True`. No ejecutar `Add-Migration` otra vez.

## Verificación posterior a SQL Server

```sql
DECLARE @EmpresaLegada uniqueidentifier =
    '09d17ec1-956c-4c6e-85b3-317c7cce4e01';

SELECT IdEmpresa, SlugNormalizado, NombreComercial, Estado
FROM dbo.Empresas WHERE IdEmpresa = @EmpresaLegada;

-- TODOS los registros actuales y posteriores en el modo mononegocio
-- deben permanecer asignados a la empresa legada.
SELECT 'Clientes' Tabla, COUNT(*) Total,
    SUM(CASE WHEN IdEmpresa <> @EmpresaLegada THEN 1 ELSE 0 END) Diferentes
FROM dbo.Clientes
UNION ALL SELECT 'Categorias', COUNT(*),
    SUM(CASE WHEN IdEmpresa <> @EmpresaLegada THEN 1 ELSE 0 END) FROM dbo.Categorias
UNION ALL SELECT 'Productos', COUNT(*),
    SUM(CASE WHEN IdEmpresa <> @EmpresaLegada THEN 1 ELSE 0 END) FROM dbo.Productos
UNION ALL SELECT 'Facturas', COUNT(*),
    SUM(CASE WHEN IdEmpresa <> @EmpresaLegada THEN 1 ELSE 0 END) FROM dbo.Facturas
UNION ALL SELECT 'DetallesFactura', COUNT(*),
    SUM(CASE WHEN IdEmpresa <> @EmpresaLegada THEN 1 ELSE 0 END) FROM dbo.DetallesFactura
UNION ALL SELECT 'Pagos', COUNT(*),
    SUM(CASE WHEN IdEmpresa <> @EmpresaLegada THEN 1 ELSE 0 END) FROM dbo.Pagos
UNION ALL SELECT 'CasosPosventa', COUNT(*),
    SUM(CASE WHEN IdEmpresa <> @EmpresaLegada THEN 1 ELSE 0 END) FROM dbo.CasosPosventa;

SELECT OBJECT_NAME(parent_object_id) Tabla, name FK
FROM sys.foreign_keys
WHERE name LIKE 'FK_%_Empresas_IdEmpresa'
ORDER BY Tabla;

SELECT t.name Tabla, c.name Columna, c.is_nullable Nullable,
       dc.name RestriccionDefault
FROM sys.columns c
INNER JOIN sys.tables t ON t.object_id = c.object_id
LEFT JOIN sys.default_constraints dc
    ON dc.parent_object_id = c.object_id
    AND dc.parent_column_id = c.column_id
WHERE t.name IN ('Clientes','Categorias','Productos','Facturas',
                 'DetallesFactura','Pagos','CasosPosventa')
  AND c.name = 'IdEmpresa'
ORDER BY t.name;
```

Los valores `Diferentes` deben ser **0** (si hay 0 filas en una tabla, `SUM` puede devolver NULL y es normal). Debe existir **una** empresa legada, **siete** columnas `IdEmpresa` no nulas, siete índices y siete FK.

## Pruebas de regresión en Nexora

- Abrir listado de clientes, categorías y productos previos.
- Editar un producto (incluida garantía) y comprobar que persiste.
- Emitir una nueva factura de prueba; visualizar el comprobante y sus meses de garantía.
- Verificar que la factura, sus detalles y su pago tengan `IdEmpresa = @EmpresaLegada`.
- Registrar una reclamación Posventa y comprobar que también tiene ese `IdEmpresa`.
- Anular factura de prueba dentro del plazo; confirmar que stock no se restituye dos veces.
- Abrir reportes, filtros e impresión; no debe cambiar la presentación.

## Por qué NO activaremos multiempresa todavía

Este paso **todavía no filtra queries por empresa**. Las escrituras actuales de controladores tampoco validan que las relaciones producto-categoría/factura-cliente/etc. compartan `IdEmpresa`. El default temporal es únicamente para compatibilidad; si una segunda empresa tuviera registros hoy, el sistema no sería seguro.

**Próximos pasos obligatorios antes de crear una segunda empresa:**
1. Resolver con Russell membresías, políticas y roles **por empresa**, no solamente roles globales.
2. Eliminar default legado e imponer empresa activa autenticada en cada escritura y modificación SQL masiva.
3. Validar consultas, `FindAsync`, informes, exportaciones e impresión contra empresa activa.
4. Crear FKs compuestas y pruebas negativas A/B de todos los módulos.
5. Solo entonces activar selector, invitaciones y alta controlada de empresas.

No fusionar el PR actual a `master` ni publicar hasta pruebas reales y revisión completa.

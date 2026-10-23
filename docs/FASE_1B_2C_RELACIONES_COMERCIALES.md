# Nexora SaaS — Fase 1B-2C: integridad relacional por empresa

**Fecha de preparación:** 21/10/2026.  
**Rama:** `feature/nexora-ui`.  
**Estado:** migración preparada, **NO compilada ni ejecutada contra la base local** en esta sesión.  
**Objetivo:** impedir desde SQL Server que una relación comercial apunte a datos pertenecientes a otra empresa.

> Esta entrega **no** abre el SaaS a múltiples negocios ni reemplaza los controles de acceso por usuario. No ejecutarla en producción. El modo Development usa todavía Nexora Legado sin autenticación por membresía.

## 1. Qué cambia

Migración: `20261021175000_AislarRelacionesComerciales`.

| Relación | Clave foránea anterior | Nueva referencia protegida |
| --- | --- | --- |
| Producto → Categoría | `IdCategoria` | `(IdEmpresa, IdCategoria)` |
| Factura → Cliente | `IdCliente` | `(IdEmpresa, IdCliente)` |
| DetalleFactura → Factura | `IdFactura` | `(IdEmpresa, IdFactura)` |
| DetalleFactura → Producto | `IdProducto` | `(IdEmpresa, IdProducto)` |
| Pago → Factura | `IdFactura` | `(IdEmpresa, IdFactura)` |
| CasoPosventa → DetalleFactura | `IdDetalle` | `(IdEmpresa, IdDetalle)` |

- Se crean **cinco** claves alternas únicas en las tablas principales para respaldar los FKs compuestos: Categorias, Clientes, Productos, Facturas y DetallesFactura.
- Se crean **seis** índices de búsqueda sobre columnas de la relación compuesta en las tablas hijas.
- Se sustituyen **seis** FKs simples por las FKs compuestas, todas con `NoAction`. La venta y anulación no dependen de cascadas; con NoAction se refuerza la conservación del historial.
- Se mantienen las claves primarias enteras, columnas de inventario, garantías, número de factura, pago y estados comerciales originales.
- Se mantienen los índices simples anteriores y el `IdEmpresa` temporal por defecto; **este default debe eliminarse antes de abrir multiempresa**.
- `Down()` quita claves/índices compuestos y recupera las FKs originales (incluyendo los antiguos cascades de DetalleFactura→Factura y Pago→Factura), sin eliminar registros.

## 2. Antes de compilar/aplicar — respaldo y condición de integridad

1. Confirmar que se trata de una **base de desarrollo** y **no** de un entorno público.
2. Respaldar la base en SQL Server y comprobar que se puede restaurar.
3. Confirmar que la migración anterior `PrepararDatosComercialesPorEmpresa` ya estaba aplicada; la consola de Visual Studio debe mostrarla con `applied = True`.
4. Ejecutar estas **consultas de solo lectura**. Cada una debe devolver 0 (cero) asociaciones con empresa distinta:

```sql
SELECT 'Producto-Categoria' Relacion, COUNT(*) Cruces
FROM dbo.Productos h INNER JOIN dbo.Categorias p ON h.IdCategoria = p.IdCategoria
WHERE h.IdEmpresa <> p.IdEmpresa
UNION ALL
SELECT 'Factura-Cliente', COUNT(*)
FROM dbo.Facturas h INNER JOIN dbo.Clientes p ON h.IdCliente = p.IdCliente
WHERE h.IdEmpresa <> p.IdEmpresa
UNION ALL
SELECT 'Detalle-Factura', COUNT(*)
FROM dbo.DetallesFactura h INNER JOIN dbo.Facturas p ON h.IdFactura = p.IdFactura
WHERE h.IdEmpresa <> p.IdEmpresa
UNION ALL
SELECT 'Detalle-Producto', COUNT(*)
FROM dbo.DetallesFactura h INNER JOIN dbo.Productos p ON h.IdProducto = p.IdProducto
WHERE h.IdEmpresa <> p.IdEmpresa
UNION ALL
SELECT 'Pago-Factura', COUNT(*)
FROM dbo.Pagos h INNER JOIN dbo.Facturas p ON h.IdFactura = p.IdFactura
WHERE h.IdEmpresa <> p.IdEmpresa
UNION ALL
SELECT 'Posventa-Detalle', COUNT(*)
FROM dbo.CasosPosventa h INNER JOIN dbo.DetallesFactura p ON h.IdDetalle = p.IdDetalle
WHERE h.IdEmpresa <> p.IdEmpresa;
```

Si se obtiene un valor mayor que cero, **no ejecutar `Update-Database`**. Investigar y corregir la discrepancia con un procedimiento seguro; **no** asignar automáticamente los registros de otra empresa al tenant heredado.

## 3. Compilar y aplicar (solo si todo lo anterior está correcto)

```powershell
git switch feature/nexora-ui
git pull origin feature/nexora-ui
```

1. Compilar con **Ctrl + Shift + B**. Si falla, no tocar la base; enviar el error completo.
2. Comprobar el servidor y la cadena de conexión activa de Visual Studio; no modificar producción.
3. En **Package Manager Console**:

```powershell
Get-Migration
Update-Database
Get-Migration
```

4. La última migración `AislarRelacionesComerciales` debe mostrar `applied=True`.

La migración vuelve a verificar que no existan relaciones cruzadas; si las encuentra, lanza un error deliberado y evita cambiar el esquema. EF Core aplica las migraciones bajo una transacción donde SQL Server lo permite.

## 4. Verificar las restricciones realmente creadas

```sql
SELECT fk.name,
       OBJECT_NAME(fk.parent_object_id) TablaHija,
       OBJECT_NAME(fk.referenced_object_id) TablaPadre
FROM sys.foreign_keys fk
WHERE fk.name IN (
 'FK_Productos_Categorias_IdEmpresa_IdCategoria',
 'FK_Facturas_Clientes_IdEmpresa_IdCliente',
 'FK_DetallesFactura_Facturas_IdEmpresa_IdFactura',
 'FK_DetallesFactura_Productos_IdEmpresa_IdProducto',
 'FK_Pagos_Facturas_IdEmpresa_IdFactura',
 'FK_CasosPosventa_DetallesFactura_IdEmpresa_IdDetalle')
ORDER BY fk.name;

SELECT OBJECT_NAME(k.parent_object_id) Tabla, k.name
FROM sys.key_constraints k
WHERE k.type = 'UQ'
  AND k.name LIKE 'AK_%_IdEmpresa_%'
ORDER BY k.name;
```

Resultado esperado: **6 FKs** compuestas y **5** restricciones únicas alternas. Para comprobar las columnas de cada FK, unir `sys.foreign_key_columns` con `sys.columns` de hija/padre y validar `IdEmpresa` + el ID correspondiente.

## 5. Pruebas funcionales obligatorias tras aplicar

- Cargar Dashboard, clientes, categorías, productos e historial de facturas anteriores.
- Editar producto de prueba; volver a abrirlo y comprobar stock, garantía y categoría.
- Crear factura nueva con cliente/producto válidos, comprobar cabecera, detalles y pago (mismo `IdEmpresa`).
- Vender última unidad y verificar «Agotado», reponer existencias y comprobar «Disponible» sin cambiar el interruptor manual.
- Anular la factura una sola vez y comprobar que el stock se restituye sin duplicación. Confirmar que no cambió el historial de garantías.
- Crear/consultar caso Posventa asociado a un detalle del mismo tenant.
- Revisar Dashboard, reportes, impresión, filtros de venta y mensajes de error.
- Pruebas adicionales de integridad A/B **solo en base de pruebas separada**: intentar asociar un cliente, categoría o detalle de otra empresa. SQL Server debe rechazarlo con violación de FK compuesta.

## 6. Limitaciones pendientes (bloquean multiempresa real)

- `IValidadorAccesoEmpresa` está preparado pero **no conectado a Identity/DI/pipeline**; la integración depende de Russell.
- El `IContextoEmpresa` activo continúa siendo un adaptador de Development a la empresa heredada.
- Valores por defecto de `IdEmpresa` deben eliminarse de EF Core y de SQL Server antes de aceptar otros negocios.
- `[Authorize]`, roles/permisos por acción y acceso a documentos no implementados en todos los controladores.
- Revisar todas las mutaciones `ExecuteUpdateAsync`, SQL crudo, jobs, exportación, facturas impresas y operaciones de stock para requerir empresa verificada.
- No fusionar PR #1 a `master` ni registrar una segunda empresa antes de completar A/B y revisión formal.

**Estado de pruebas locales:** pendiente de resultados del usuario.

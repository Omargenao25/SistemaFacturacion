# NEXORA — Fase 8.6.16: pruebas POST de edición y eliminación multiempresa

**Estado:** plan de prueba; pendiente de ejecutar y verificar en Nexora Lab.

## Alcance

Verificar que un Propietario de empresa B no pueda modificar ni eliminar un producto de empresa A enviando una solicitud POST con un identificador ajeno. Ejecutar solamente contra `NexoraSecurityLab_Phase8`. No tocar master ni la base principal.

## Salvaguardas previas

1. Confirmar en Visual Studio el perfil `Nexora Lab` y la cadena de conexión `Database=NexoraSecurityLab_Phase8`.
2. Conservar los datos ya probados: Laptop Prueba A (producto ID 1, stock 10) y Monitor Prueba B (producto ID 2, stock 13).
3. Para las primeras pruebas, **no enviar POST reales**. Revisar el código del controlador y los filtros EF Core, y preparar una base temporal independiente si se desea un test destructivo.
4. Las acciones `ProductosController.Edit(int id, Producto producto)` y `DeleteConfirmed(int id)` recuperan el registro usando `_context.Productos.FirstOrDefaultAsync(p => p.IdProducto == id)`, que aplica el filtro global de empresa. Ambas deben devolver `NotFound()` si se consulta un ID ajeno.
5. El POST de edición requiere además `RowVersion`, categoría válida y validación antifalsificación; un resultado de validación por alguno de esos requisitos **no basta** para demostrar aislamiento.

## Verificación segura de lectura antes de POST

En sesión de `propietario-b@nexora-lab.test` comprobar que:
- `GET /e/nexora-lab-b/Productos/Edit/1` está bloqueado.
- `GET /e/nexora-lab-b/Productos/Delete/1` está bloqueado.

Estas dos pruebas ya se reportaron como bloqueadas en Fase 8.6.15.

## Auditoría de datos de solo lectura para conservar línea base

```sql
IF DB_NAME() <> N'NexoraSecurityLab_Phase8'
    THROW 51330, 'Base incorrecta: solo laboratorio.', 1;

SELECT e.SlugNormalizado, p.IdProducto, p.Codigo, p.Nombre, p.Stock
FROM dbo.Productos p
JOIN dbo.Empresas e ON e.IdEmpresa = p.IdEmpresa
WHERE p.Codigo IN (N'LAB-A-001', N'LAB-B-001')
ORDER BY e.SlugNormalizado;

SELECT e.SlugNormalizado, COUNT_BIG(*) AS TotalFacturas
FROM dbo.Facturas f
JOIN dbo.Empresas e ON e.IdEmpresa = f.IdEmpresa
WHERE e.SlugNormalizado IN (N'nexora-lab-a', N'nexora-lab-b')
GROUP BY e.SlugNormalizado;
```

## Criterios para aprobar una futura prueba POST

- Una solicitud POST autenticada, con antiforgery válido y datos de formulario coherentes intenta operar sobre un ID ajeno.
- Se recibe `404` o un rechazo explícito del servidor **por ausencia de registro visible**, no simplemente un error de token/campo faltante.
- Auditoría SQL antes/después confirma que producto A mantiene valores originales y no hay cambios en facturas ni relaciones.
- Si la operación modifica cualquier dato o hay resultados ambiguos, detener pruebas e investigar antes de repetir.

## Próximo paso sugerido

Crear una prueba automatizada ejecutada contra **una copia descartable del laboratorio**, con limpieza/rollback y aserciones de no modificación. No ejecutarla sobre el laboratorio que contiene la factura y los productos actuales.

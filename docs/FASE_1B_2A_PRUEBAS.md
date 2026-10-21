# Nexora SaaS — Fase 1B-2A: alcance inicial de empresa en EF Core

**Estado:** código en `feature/nexora-ui`; sin compilación ni prueba real desde GitHub.

## Objetivo y límites

Se añadió una capa **provisional** que filtra registros comerciales por la empresa activa:

- `IContextoEmpresa`: contrato de empresa resuelta por servidor.
- `ContextoEmpresaDesarrollo`: solo en el entorno `Development` devuelve la empresa heredada `09d17ec1-956c-4c6e-85b3-317c7cce4e01`. En otros entornos devuelve `Guid.Empty`.
- Las 7 entidades comerciales implementan `IEntidadEmpresa`.
- `ApplicationDbContext` configura `HasQueryFilter` para `Clientes`, `Categorias`, `Productos`, `Facturas`, `DetallesFactura`, `Pagos` y `CasosPosventa`.
- `SaveChanges` / `SaveChangesAsync`: asignan el tenant a entidades nuevas y **rechazan** modificaciones y eliminaciones que no pertenezcan al tenant. Identidad y tablas globales de plataforma quedan fuera de esta validación.
- Los controladores Clientes/Categorías editan primero cargando el registro filtrado, sin `_context.Update` de una entidad recibida por POST.
- Los `FindAsync` de Clientes, Categorías, Productos y Posventa se sustituyeron por búsquedas LINQ filtrables por EF.
- Facturación valida que `IdCliente` corresponda a un cliente de la empresa antes de crear la factura.
- `ExecuteUpdateAsync` normal de EF aplica los filtros globales de consulta, pero **no pasa por SaveChanges**, por lo cual se requieren pruebas específicas y alcance explícito en cada operación sensible.

**No es todavía aislamiento SaaS apto para producción.**
No están implementados todavía:
1. `IContextoEmpresa` real a partir de usuario autenticado, membresías, empresa activa y permisos: depende de coordinación con Russell.
2. FKs compuestas `(IdEmpresa, Id...) ` que impiden relaciones comerciales cruzadas en SQL Server.
3. Quitar valores por defecto `nexora-legado` en EF y SQL Server.
4. Filtros y autorización de archivos/PDF, consultas crudas, background jobs e integraciones futuras.
5. Pruebas de aislamiento A/B con dos empresas y usuarios distintos.

**Nunca publicar esta versión ni usarla para albergar dos empresas reales.**
**El entorno Development siempre ve la empresa heredada** aunque no haya usuario autenticado. Por eso debe mantenerse local y aislado de internet. Fuera de Development no hay empresa resuelta: las consultas comerciales normales quedan vacías y las escrituras por `SaveChanges` se rechazan. Esto **no sustituye** autorización por ruta ni seguridad fiscal.

## Preparación local

1. Respaldar la base de desarrollo. Confirmar que `PrepararDatosComercialesPorEmpresa` figura aplicada en `Get-Migration`.
2. En Terminal:

```powershell
git switch feature/nexora-ui
git pull origin feature/nexora-ui
```

3. Compilar `Ctrl+Shift+B` y detener la ejecución si aparece algún error. Compartir el error completo antes de cualquier cambio SQL.
4. **No ejecutar Add-Migration**. Esta etapa modifica solo código EF/validaciones y no agrega nueva migración de base de datos.
5. Ejecutar en `Development` con `F5` y verificar que la instancia es estrictamente local.

## Prueba funcional: mononegocio existente

| Prueba | Resultado esperado |
| --- | --- |
| Abrir Dashboard/Clientes/Categorías/Productos | Mismos registros previos de Nexora Legado |
| Crear cliente y editar sus datos | Guardar correctamente, conservando `IdEmpresa` |
| Crear y editar categoría | Guardar correctamente, conservando `IdEmpresa` |
| Crear y editar producto y su garantía | Funciona; stock y RowVersion siguen protegidos |
| Emitir factura seleccionando cliente y producto válidos | Factura, detalles y pago asignados a empresa heredada |
| Ver historial, comprobar garantías e imprimir | Sin cambio de diseño |
| Anular factura de prueba una vez | Stock vuelve una sola vez |
| Crear y cerrar caso Posventa de prueba | Caso relacionado con empresa heredada |
| Reportes de ventas y stock | Solo muestran datos de la empresa heredada |

## Pruebas de seguridad previstas (no ejecutar contra producción)

- Verificar por logging de SQL EF que SELECT sobre productos, clientes, facturas, pagos, detalle y posventa añade `WHERE IdEmpresa = ...`.
- Intentar enviar `IdCliente` que no exista en el tenant actual en POST de crear factura: debe mostrar error y no generar factura.
- Revisar que no quede ningún `_context.Update(cliente/categoria)` desde valores enviados por POST.
- Verificar `SaveChanges`/Async al cambiar `IdEmpresa` manualmente: rechaza el cambio.
- En un entorno **de pruebas aislado**, comprobar que contexto con `Guid.Empty` no devuelve datos comerciales y rechaza altas; no ejecutar esto sobre la base principal.
- Auditoría de código: buscar `IgnoreQueryFilters`, `FromSqlRaw`, `ExecuteSqlRaw`, `ExecuteUpdateAsync`, `ExecuteDeleteAsync` y asegurar que ninguna ruta omite el tenant.
- No simular una segunda empresa real hasta implementar todos los controles de la fase 1B completa.

## Criterios pendientes de aceptación

- [ ] Compilación real .NET 8
- [ ] Clientes/Categorías/Productos funcionando en Development
- [ ] Factura emitida y anulada, garantía correcta, sin stock duplicado
- [ ] Posventa y Reportes regresión
- [ ] Inspección del SQL con predicados por empresa
- [ ] Probar falla cerrada en ambiente de pruebas separado
- [ ] Integración con Russell y membresía real
- [ ] Claves foráneas compuestas e índices
- [ ] Eliminar default temporal de SQL y EF
- [ ] Pruebas A/B de aislamiento empresarial y validación de seguridad

## Próxima entrega

Fase 1B-2B: diseñar e integrar la resolución de empresa real (Identity + `EmpresaUsuario` + permiso por acción, en conjunto con Russell). La base de datos debe proteger de modo fuerte las relaciones entre empresas y rechazar cualquier referencia cruzada. No activar segundo tenant mientras quede algún endpoint sin aislamiento.

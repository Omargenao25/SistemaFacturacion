# Nexora — Revisión preventiva para la próxima sesión (20 oct 2026)

**Fuente:** inspección estática del código actual en `feature/nexora-ui`.
**Limitación:** no se ejecutó `dotnet build`, no hubo pruebas locales, no se accedió a la base SQL Server y no se tocó `master`.
**Decisión:** revisión y planificación solamente. No hacer modificaciones delicadas a la facturación ni a Identity mientras el usuario está ausente.

## 1. Inventario agotado: comportamiento ya implementado

- `Models/Producto.cs`: `Estado` es el control manual persistido.
- Propiedades `[NotMapped]`: `Agotado` (`Stock <= 0`), `DisponibleParaVenta`, `EstadoInventario`, `DisponibilidadVenta` y clases de badge. Ninguna actualiza la base de datos.
- `Controllers/FacturasController.cs`, `CargarListas()`: el selector limita los productos a `Estado && Stock > 0`.
- La venta usa `ExecuteUpdateAsync` con condición `p.Estado && p.Stock >= cantidad`, `RowVersion` y transacción. Así, cuando se vende la última unidad, el producto sigue **manualmente activo**, pero aparece **Agotado** al recargar.
- La anulación suma el stock por `ExecuteUpdateAsync` después de comprobar y cambiar una sola vez el estado de la factura dentro de la transacción; el indicador visual vuelve a calcularse al consultar.
- El catálogo y las pantallas de producto muestran por separado el estado automático de existencias y la habilitación manual.
- **Ninguna recompilación ni prueba funcional de esta entrega ha sido confirmada por el usuario todavía.**

## 2. Pruebas que debe realizar el usuario antes de más cambios

Referencia: [`ESTADO_INVENTARIO_AUTOMATICO_PRUEBAS.md`](ESTADO_INVENTARIO_AUTOMATICO_PRUEBAS.md).

| Caso | Operación | Resultado esperado |
| --- | --- | --- |
| Última unidad | Producto activo con 1 unidad, vender 1 | Stock 0, inventario Agotado, manual Activo, fuera del selector de facturas |
| Reposición | Editar stock 0 → 8 en producto activo | Disponible automáticamente, sin tocar Estado |
| Retiro de catálogo | Poner Estado = false, stock 8 | Inventario Disponible, control manual Inactivo, no vendible |
| Reposición inactiva | Stock 0 → 8 con Estado = false | Sigue Inactivo para facturar; nunca se reactiva automáticamente |
| Anulación | Anular una factura emitida de prueba dentro del plazo | Se devuelve stock una vez, badge refleja nuevo stock |
| Concurrencia | Dos ventas intentan agotar la misma última unidad | La segunda no emite venta, no hay stock negativo |
| Regresión | Productos, clientes, garantías, posventa y reportes | Sin errores ni cambio de datos históricos |

**No requiere `Update-Database`** para los estados automáticos.

## 3. Riesgos de SaaS que aún BLOQUEAN producción y la segunda empresa

### Bloqueante A — Identificación de empresa sin autenticación real

`Services/Tenancy/ContextoEmpresaDesarrollo.cs` selecciona la empresa legada para **cualquier solicitud en Development**, sin comprobar el usuario. Esto es únicamente un adaptador local; **nunca** desplegar Development en internet ni habilitar acceso de terceros.

En entornos no Development retorna `Guid.Empty`, por lo que el flujo comercial aún **no está listo para producción**. Integrar la identidad/membresía/roles por empresa en coordinación con Russell, no duplicar su módulo.

### Bloqueante B — Aislamiento completo de relaciones comerciales

`Data/ApplicationDbContext.cs` aplica `HasQueryFilter` por `IdEmpresa` a siete entidades y revisa `SaveChanges`. Pero aún existe un default legado en EF/SQL Server. Debe retirarse **antes de admitir otros tenants**.

Crear claves e índices compuestos por `(IdEmpresa, IdEntidad)` para **evitar FKs entre empresas diferentes**: Factura → Cliente, Detalle → Factura/Producto, Producto → Categoria, Pago → Factura y Posventa → Detalle. Los índices de consulta deben incorporar empresa donde aplique.

### Bloqueante C — Operaciones fuera de SaveChanges

`ExecuteUpdateAsync` en `FacturasController` no pasa por la protección `ValidarEscriturasDeEmpresa()`. Las consultas EF normales reciben el filtro global, pero es indispensable incluir el alcance por tenant en las condiciones de escritura **explícitamente** y realizar pruebas con registros de empresas A/B. Revisar también operaciones SQL directas, `IgnoreQueryFilters`, informes, exportaciones y trabajos en segundo plano.

### Bloqueante D — Autorización en rutas MVC

Los controladores comerciales aún no tienen autorización por membresía y rol implementada en todos los endpoints. No basta con filtrar la base de datos: autenticar, comprobar empresa activa y permisos de cada operación (por ejemplo, quién puede crear, anular, editar stock y resolver una devolución).

## 4. Próxima entrega técnica recomendada (cuando el equipo retome)

**Objetivo: Fase SaaS 1B-2B, diseño integrado con Russell**. Orden seguro:

1. Confirmar con Russell si utilizará `IdentityUser` o un usuario de aplicación extendido, y cómo gestiona los roles actuales. Evitar conflictos de `Program.cs`/Identity.
2. Definir contratos de `IContextoEmpresa` respaldados por `EmpresaUsuario` activa, mediante usuario autenticado y empresa solicitada autorizada; **nunca confiar en un `EmpresaId` recibido por formulario**.
3. Crear tests negativos A/B (GET/POST/editar/eliminar/facturar/anular/reportes/posventa) con identidad y SQL Server de pruebas.
4. Diseñar migración reversible con respaldo, validación previa de integridad y FKs compuestas; primero comprobar relaciones existentes.
5. Retirar defaults legados y permitir un segundo tenant solamente **después** de pasar las pruebas anteriores.
6. Mantener PR #1 en borrador hasta auditoría, compilación, regresión y aprobación explícita.

## 5. Estado y entrega

- Esta revisión **no es una prueba aprobada** ni equivale a certificación de seguridad.
- No se modificó facturación, Identity, datos SQL Server, estados comerciales ni seguridad mientras el usuario duerme.
- Los cambios anteriores siguen en `feature/nexora-ui`. El usuario debe verificar primero las pruebas de Agotado/Reposición/Manual descritas arriba.
- Si una prueba falla, priorizar corrección y regresión antes de continuar con la arquitectura SaaS.

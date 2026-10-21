# Nexora — Estado automático de inventario sin alterar la desactivación manual

**Estado:** desarrollado en `feature/nexora-ui`; **pendiente de compilar y validar localmente**.

## Decisión de producto

Separar dos dimensiones independientes:

- `Producto.Estado` (booleano persistido): **habilitación manual** del administrador. Solo un cambio explícito de catálogo cambia este valor.
- `Producto.Stock` (entero persistido): existencias reales.
- `Producto.EstadoInventario` (calculado, **no persistido**): `Agotado` cuando `Stock <= 0`, `Stock bajo` de 1 a 5 unidades y `Disponible` desde 6.
- `Producto.DisponibilidadVenta` (calculado, **no persistido**): `Inactivo` si el administrador desactivó el producto; en otro caso refleja el estado de inventario.
- `DisponibleParaVenta` (calculado): `Estado && Stock > 0`.

Las propiedades de solo lectura llevan `[NotMapped]`; **no hay migración SQL** ni columna adicional. Los cambios se reflejan al volver a consultar la base (recargar pantalla), incluso cuando el stock cambió por facturación o anulación atómica.

**Importante:** Agotado NO pone `Estado = false`. Una reposición nunca fuerza `Estado = true`. Si un artículo estaba manualmente inactivo, seguirá inactivo tras reponer y no se podrá facturar.

## Cambios visibles

- Productos / listado: columna «Estado del inventario» con insignias `Agotado`, `Stock bajo` o `Disponible`; columna distinta «Control manual» con `Activo/Inactivo`.
- Productos / Detalles: «Estado del inventario (automático)», «Disponibilidad para facturar» y «Control manual» diferenciados.
- Productos / Crear y Editar: aclaraciones sobre 0 unidades, reposición y desactivación manual.
- Productos / Eliminar: añade estado de inventario.
- Dashboard y Reportes: alertas de stock bajo/agotado consistentes con los cálculos del modelo.
- Facturas / Crear: **se conserva la protección anterior**: el selector solo carga productos `Estado && Stock > 0`, y la venta verifica existencia y estado en SQL con `ExecuteUpdateAsync` y transacción. No se modificaron esas protecciones ni la anulación segura.

## Pruebas recomendadas en Visual Studio

1. Verificar que estás en `feature/nexora-ui`, ejecutar `git pull origin feature/nexora-ui`, compilar `Ctrl+Shift+B`.
2. **No ejecutar `Add-Migration` ni `Update-Database` para esta entrega.** Ejecutar con F5 en Development local.
3. Crear un producto de prueba **activo**, con `Stock = 1`, y comprobar «Stock bajo» y «Activo».
4. Facturar exactamente **1 unidad**. Volver a Productos/Index (refrescar). Debe mostrar `0`, **Agotado**, **Activo** (control manual intacto), y el producto no debe aparecer en «Nueva factura».
5. En Editar, registrar `Stock = 8` y guardar. Debe aparecer **Disponible**, **Activo**, volver al selector de facturas.
6. Marcar manualmente **Inactivo**, sin cambiar `Stock = 8`. El listado debe decir «Disponible» (stock físico) y «Inactivo» (control manual). La ficha muestra disponibilidad para facturar «Inactivo»; no aparece en la venta.
7. Poner `Stock = 0` con el producto **Inactivo**. Debe mostrar «Agotado» (stock físico) e «Inactivo» (control manual).
8. Reponer `Stock = 8` dejando **Inactivo**. Debe seguir **Inactivo** y oculto del selector de facturas.
9. Solo cuando un administrador vuelva a marcar **Activo**, debe permitir facturarlo de nuevo.
10. Probar anulación de una factura de prueba emitida sobre producto activo con stock 0: la restitución de stock lo devuelve a `Stock bajo` o `Disponible` automáticamente. Comprobar que el stock se restituye una sola vez.
11. Comprobar que garantías, reportes, Dashboard, historial y Posventa siguen funcionando.

## Criterios de aceptación

| Estado manual | Stock | Estado inventario | Disponibilidad para facturar |
| --- | ---: | --- | --- |
| Activo | 0 | Agotado | Agotado (no vendible) |
| Activo | 1–5 | Stock bajo | Stock bajo (vendible si hay suficientes unidades) |
| Activo | 6+ | Disponible | Disponible |
| Inactivo | 0 | Agotado | Inactivo (no vendible) |
| Inactivo | 1–5 | Stock bajo | Inactivo (no vendible) |
| Inactivo | 6+ | Disponible | Inactivo (no vendible) |

Revisar especialmente que ninguna operación automática escriba `Estado` al vender, anular o actualizar existencias.

**Seguridad SaaS:** persisten los bloqueos ya documentados de la fase 1B. No usar con múltiples empresas reales ni exponer el modo Development a internet hasta tener autenticación, aislamiento completo y pruebas A/B.

**Resultado real de compilación / pruebas:** pendiente de confirmación del usuario.

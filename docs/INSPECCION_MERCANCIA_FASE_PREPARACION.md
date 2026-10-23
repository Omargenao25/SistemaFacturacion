# Nexora — Preparación del módulo de recepción e inspección (solo lectura)

**Estado:** código preparado en `feature/nexora-ui`. **No probado mediante compilación ni navegador/SQL Server desde el asistente.** El usuario debe verificarlo en su Visual Studio. No está habilitada la recepción física real.

## Alcance implementado

- Menú **Recepciones** en `Views/Shared/_Layout.cshtml`.
- `GET /Recepciones`: bandeja de recepciones con filtro de estado, producto, número de factura, cantidades esperadas/recibidas y enlaces a la ficha.
- `GET /Recepciones/Details/{id}`: datos de la obligación física y de su venta de origen, pendientes y liberadas (los valores **persistidos**, sin cambios).
- `GET /Recepciones/Details/{id}?simular=true&cantidadNueva=...&cantidadApta=...&cantidadCuarentena=...&cantidadReparacion=...&cantidadDescartada=...`: simulador que **no guarda nada**. Solo valida las cantidades y devuelve resultados en la pantalla.
- Enlace **Simular inspección** desde las recepciones de `Views/Facturas/Details.cshtml`.
- Estilos responsive en `wwwroot/css/site.css`, diferenciando claramente simulación de operación real.
- Reglas puras en `Services/Inventario/ReglasInspeccionMercancia.cs` y modelo de pantalla `ViewModels/InspeccionRecepcionViewModel.cs`.

**No se modifica ninguna entidad, tabla, configuración EF, migración, contexto de empresa, Identity de Russell, factura, stock o reembolso.** No se añade ningún POST de recepción, autorización implícita ni botón para ejecutar cambios financieros.

## Política de inspección propuesta (sin activar)

Una recepción física autorizada debería permitir una **cantidad nueva recibida**, y clasificar cada unidad en una de estas categorías excluyentes:

| Categoría | Implicación de negocio prevista |
| --- | --- |
| **Apta para venta** | Puede proponerse para liberar stock **solo después** de validación del responsable; no se libera desde el simulador |
| **Cuarentena** | Se mantiene retenida, pendiente de revisión; no se vende |
| **Reparación** | Se mantiene fuera de stock vendible hasta intervención e inspección posterior |
| **Descarte** | No se reincorpora a venta; requiere trazabilidad y gestión apropiada |

Las reglas `ReglasInspeccionMercancia.Evaluar` verifican:

1. `CantidadEsperada > 0` y `0 <= CantidadYaRecibida <= CantidadEsperada`.
2. `CantidadNueva > 0`, sin exceder `CantidadEsperada - CantidadYaRecibida`.
3. Todas las cantidades de categorías deben ser no negativas.
4. `CantidadApta + CantidadCuarentena + CantidadReparacion + CantidadDescartada == CantidadNueva`.
5. Resultado válido: calcular **aptas propuestas**, **retenidas propuestas** y **pendientes hipotéticas**; nunca aumentar `Producto.Stock` ni modificar `RecepcionAnulacion`.
6. Se usan sumas `long` al validar para prevenir desbordamiento por parámetros GET extremos.

## Pruebas de simulación propuestas

| Esperadas | Ya recibidas | Nuevas | Aptas / Cuarentena / Reparación / Descarte | Esperado |
| ---: | ---: | ---: | --- | --- |
| 5 | 0 | 3 | 2 / 1 / 0 / 0 | Válida; aptas propuestas 2, retenidas 1, pendientes 2 |
| 2 | 0 | 3 | 3 / 0 / 0 / 0 | Rechazo por exceder pendientes |
| 4 | 0 | 2 | 1 / 0 / 0 / 0 | Rechazo: clasificación suma 1, no 2 |
| 4 | 0 | 2 | -1 / 3 / 0 / 0 | Rechazo por valores negativos |
| 1 | 0 | 0 | 0 / 0 / 0 / 0 | Rechazo: debe haber alguna unidad |
| 6 | 2 | 3 | 1 / 1 / 1 / 0 | Válida; pendientes hipotéticas 1 |
| 6 | 2 | 5 | 4 / 1 / 0 / 0 | Rechazo: faltan 4 por recibir |

## Revisión en Visual Studio

1. `git switch feature/nexora-ui`
2. `git pull origin feature/nexora-ui`
3. `Ctrl + Shift + B` para compilar. **No ejecutar `Update-Database`: no hay migraciones nuevas**.
4. Ejecutar con `F5` en una base **Development**, nunca pública.
5. Abrir **Recepciones** desde el menú. Deben aparecer las anulaciones recientes que ya generaron recepciones pendientes.
6. Abrir una recepción con **Revisar**: debe mostrar la factura, producto y cantidades reales.
7. Ensayar la clasificación válida de la primera fila; debe devolver resultado verde **solo simulado**.
8. Ensayar clasificaciones de suma incorrecta y de exceso; deben devolver advertencias.
9. Abrir de nuevo la ficha sin parámetros o refrescar desde el menú: **las cantidades reales recibidas/liberadas siguen en 0 y el estado Pendiente**.
10. Comprobar que `Productos.Stock`, facturas, pagos y reembolsos no hayan cambiado. El botón de recibo físico **no existe**.
11. Desde el detalle de una factura anulada nueva, usar **Simular inspección** en la tabla de mercancía.
12. Revisar en móvil o con ventana estrecha y volver a probar navegación.

## Límites y pendientes críticos para SaaS real

- El contexto `ContextoEmpresaDesarrollo` usa empresa heredada y no dispone todavía de autorización por membresía. Estos GET muestran información comercial en Development: **NO publicar ni dejar esa instancia accesible públicamente**.
- Integrar Identity, roles por empresa, y control granular de permisos con Russell antes de cualquier POST de recepción o salida de fondos.
- Crear registro inmutable de eventos de recepción física con inspector, sello de tiempo UTC, condición, evidencia, y trazabilidad de cantidades. Preservar historial de correcciones.
- Manejar concurrencia: múltiples operadores, idempotencia y prevención de doble liberación.
- Registrar movimientos de inventario y separar cuarentena, reparación, descarte y unidades aptas.
- Conectar la aprobación del reembolso con la conciliación del medio de pago, no mezclar dinero con recepción física.
- Analizar devoluciones parciales y casos posventa que no requieren anular factura.
- No fusionar PR #1 a `master`, no crear segundos tenants ni habilitar producción.

**Fase completada en código:** lectura y simulación. **Fase pendiente:** recepción real autorizada y auditoría.

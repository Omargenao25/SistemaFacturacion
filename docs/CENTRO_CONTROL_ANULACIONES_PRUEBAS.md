# Nexora — Centro de control de anulaciones (solo lectura)

**Rama:** `feature/nexora-ui`. **Estado:** implementación preparada; requiere compilación y pruebas en Visual Studio / SQL Server local antes de darla por validada.

## Objetivo

Una empresa no debe investigar en pantallas desconectadas qué ocurrió con una factura anulada. Este panel muestra el seguimiento financiero y físico **sin crear movimientos ni suponer que ocurrieron**:

- **Una fila por factura anulada** (NO una fila por producto ni por pago).
- **Reembolso:** estado persistido, importe de solicitud y método original de cobro; *no acredita una devolución de dinero ni una nota de crédito*.
- **Recepciones:** estado derivado de registros por línea de factura, cantidad total pendiente conocida y cobertura de registros. Ninguna recepción se confirma desde esta pantalla.
- Anulaciones antiguas sin `Reembolso` o `RecepcionAnulacion` aparecen como **Sin registro** o **Revisar registros**. No se inventan eventos ni se altera stock histórico.
- Búsqueda por número de factura o nombre/apellido del cliente y paginación real del servidor de **25 facturas**. El total de facturas encontradas se calcula sobre el conjunto filtrado.
- Los cuatro KPIs de la pantalla son **de la página visible**, no valores globales. `MontoSolicitudesAbiertas` agrega solamente importes de solicitudes registradas cuyo estado es distinto de `Reembolsado` y `Rechazado`, sin declararlos deuda legal ni pago efectivamente saliente. Solicitudes `PendienteRevision` entran en este agregado, sujetas a conciliación.
- Si faltan registros de recepción para alguna línea de la factura, **no** estimar las unidades pendientes totales: el estado pasa a `Revisar registros` y el valor de unidades pendientes queda desconocido. El KPI físico suma solo datos completos.
- Los accesos desde la tabla van a `Facturas/Details` para consultar el registro original y sus paneles; desde allí se puede abrir la simulación de inspección.

## Cambios incluidos

- `ViewModels/ControlAnulacionesViewModel.cs`: proyección de lectura, estados, indicadores y resumen por factura.
- `Controllers/ControlAnulacionesController.cs`: **única acción GET**. Consulta `Facturas`, `Reembolsos`, `DetallesFactura` y `RecepcionesAnulacion` con sus filtros de empresa intactos y sin `IgnoreQueryFilters`. Página de 25 en SQL Server antes de consultar dependencias, sin multiplicar dinero por cantidad de artículos.
- `Views/ControlAnulaciones/Index.cshtml`: panel profesional con búsqueda, indicadores explicados, tabla responsive y navegación de páginas.
- `Views/Shared/_Layout.cshtml`: enlace **Control de anulaciones** dentro de Operaciones.
- `wwwroot/css/site.css`: diseño del panel de seguimiento.

**No hay entidad nueva, ni migración, ni cambios en los controladores de venta/anulación/posventa, ni cambios en Identity, ni escrituras SQL. No ejecutar `Update-Database`.**

## Pruebas locales — Visual Studio

1. Actualiza `feature/nexora-ui`: `git switch feature/nexora-ui` y `git pull origin feature/nexora-ui`.
2. Compila con **Ctrl + Shift + B** y envía cualquier error completo antes de seguir.
3. Inicia en **Development local con F5**, nunca con una instancia pública anónima.
4. Abre **Control de anulaciones** en el menú. Solo deben aparecer facturas con estado **Anulada**, con número, cliente, importe original y los seguimientos separados.
5. Selecciona una anulación reciente con **2 o más productos**. El reembolso pendiente debe mostrarse **una sola vez**, sin multiplicar su importe por las líneas.
6. Revisa la anulación que creaste cuando probamos las recepciones: debe verse `Pendiente` en mercancía, sus unidades por recibir y `Pendiente` o `PendienteRevision` en el registro financiero.
7. Busca un número o nombre de cliente, presiona Buscar y Limpiar. Debe respetar el filtro de facturas anuladas.
8. Si tienes más de 25 facturas anuladas, comprueba **Anterior / Siguiente** y que los KPI cambian exclusivamente para cada página.
9. Una factura anulada **antes de introducir reembolsos y recepciones** debe indicar los registros faltantes; no debe afirmar que se devolvió dinero ni mercadería.
10. Confirma que enlaces **Abrir** y navegación llevan al detalle existente. Revisa en ventana estrecha.
11. Comprueba que `Productos.Stock`, `Facturas`, `Reembolsos` y `RecepcionesAnulacion` permanecen **sin cambios** al buscar, filtrar, paginar y abrir el panel.

## Casos técnicos y seguridad pendientes

- Los valores `Reembolsado` y `Completada` reflejarían estados **registrados**, no sustituyen evidencia bancaria ni inspección física; aún no existen endpoints de confirmación autorizados.
- No agregar exportaciones de datos personales, botones administrativos ni acciones POST mientras no se integren los permisos per-tenant y la auditoría.
- En entornos mayores, plantear dashboard agregado por SQL, pruebas de concurrencia, índices operativos y una política de paginación/filtración adecuada. Los KPI actualmente no son globales por diseño.
- La autenticación multiempresa de Russell y la protección de accesos GET siguen siendo bloqueantes para entornos públicos.
- No fusionar PR #1, no activar segunda empresa y no desplegar sin completar seguridad.

**Entregable de esta fase:** tablero informativo de anomalías y pendientes, sin habilitar ninguna operación sensible.

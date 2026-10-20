# Nexora — Plan de pruebas de atención posventa

> Implementación preliminar, NO despliegue productivo. Compilar y probar en SQL Server local antes de fusionar.

## Preparación

1. Confirmar rama `feature/nexora-ui`; respaldar base de datos de desarrollo.
2. Ejecutar `git pull origin feature/nexora-ui`.
3. Compilar con Ctrl+Shift+B.
4. En Package Manager Console, seleccionar proyecto y cadena de conexión correctos; ejecutar `Update-Database`.
   Esta migración es `20261020234000_CrearModuloPosventa` (ya se incluyó en el repositorio, NO ejecutar `Add-Migration` para recrearla).
5. Abrir Nexora desde Visual Studio con F5.

## Casos de prueba

| Prueba | Resultado esperado |
| --- | --- |
| Abrir factura emitida y pulsar “Solicitar devolución o garantía” en un producto | Se abre un formulario asociado al renglón correcto |
| Reclamar 1 unidad de una venta de 2, con motivo detallado | Se registra PV-NNNNN en estado Abierto; no cambia factura ni stock |
| Abrir Posventa desde el menú lateral | El nuevo caso aparece y se puede abrir |
| Caso Abierto → En revisión → Resuelto, con observación de cierre | El estado cambia y se conserva la última observación |
| Cambiar un caso a Rechazado | Se cierra el caso sin mover inventario ni dinero |
| Solicitar más unidades que las facturadas | El controlador rechaza la solicitud |
| Crear varias devoluciones que suman más unidades que las vendidas | La última solicitud se rechaza; verificar también con dos pestañas |
| Crear dos reclamaciones de garantía activas que suman más unidades que las vendidas | La segunda se rechaza |
| Presentar un caso cuando terminó la garantía comercial | Permite abrirlo para revisión, sin afirmar aceptación automática |
| Intentar crear caso sobre factura anulada | Bloqueado |
| Abrir el detalle de una factura anterior | Comprobante e impresión continúan funcionando |
| Dos usuarios actualizan al mismo tiempo un caso con la misma versión | El segundo cambio se rechaza por conflicto |
| Comprobar tabla `CasosPosventa` y FK | Datos íntegros; la migración creó la tabla con `NO ACTION` |

## Limitaciones y pendientes

- Estado Resuelto significa **cierre administrativo del caso**, no una devolución efectiva de dinero.
- No se hacen reembolsos, notas de crédito, compensaciones, sustituciones ni ajustes de inventario.
- El sistema aún no tiene una tabla de movimientos y auditoría exhaustiva de cada cambio de estado.
- **Antes de producción**, aplicar los permisos de roles Administrador/Cajero desarrollados por Russell al controlador de Posventa; actualmente el prototipo sigue el acceso de los demás módulos.
- Una solicitud no reemplaza la valoración del cumplimiento de las leyes de protección al consumidor.
- Sin compilación ni pruebas ejecutadas en el equipo del desarrollador desde GitHub: confirmar resultados reales.

# NEXORA — Hoja de ruta para convertirlo en software comercial

**Estado:** diagnóstico inicial del código en `feature/nexora-ui`, 19/10/2026.  
**Tecnología actual:** ASP.NET Core MVC .NET 8, EF Core, SQL Server, ASP.NET Identity.  
**Objetivo:** evolucionar de demostración universitaria a **SaaS multiempresa** (empresas independientes, cada una con sus datos, usuarios y permisos), seguro, auditable, automatizable y desplegable; primero en un piloto controlado.

**Decisión de producto confirmada 19/10/2026:** Nexora será **SaaS**. Consultar [Arquitectura SaaS](docs/ARQUITECTURA_SAAS.md) y [Módulo Empresas y equipos](docs/MODULO_EMPRESAS_Y_EQUIPOS.md). Estos documentos son diseño, **no funcionalidades activadas**. La migración y aislamiento son requisitos bloqueantes para registrar nuevas empresas.  
**Importante:** esta es una hoja de ruta; **los módulos indicados como pendientes no están implementados ni probados**.

## 0. Evidencia de lo existente

| Área | Estado actual observado | Pendientes relevantes |
| --- | --- | --- |
| Catálogos | CRUD de clientes, categorías y productos; garantía configurable; `RowVersion` en productos | SKU único, reglas de entrada de datos, proveedor/marca/serie, importación y auditoría |
| Facturas | Crear, consultar, imprimir comprobante interno, anular, historial y filtros | numeración única y continua según uso; gestión fiscal conforme DGII; impuestos parametrizables; pago parcial, validación de cliente y venta |
| Inventario | Descuento/restitución atómica al facturar/anular; stock actual en `Producto` | libro de movimientos, compras/recepciones, transferencias, ajustes autorizados, conteos, ubicaciones, trazabilidad |
| Pagos | Pago creado al emitir factura, con método y monto | caja, conciliación, cobros parciales y mixtos, cuentas por cobrar, devoluciones y reembolsos autorizados |
| Posventa | `CasoPosventa` vinculado a línea de factura, seguimiento de estado y motivo | flujo real de devolución física, inspección, reparaciones, reemplazo, nota de crédito, reembolso, auditoría |
| Reportes | Ventas/ITBIS estimados, productos vendidos, stock bajo, costos/margen estimado | conciliación financiera, devoluciones, reportes por cajero, exportación, comparativas, permisos de datos |
| Seguridad | Identity configurado; trabajo de roles asignado a Russell, aún no integrado en esta rama | atributos `[Authorize]` y permisos por acción, registro limitado/deshabilitado según modo comercial, gestión de secretos, auditoría |
| Operaciones | App MVC ejecutable localmente según pruebas del usuario | CI, tests, despliegue reproducible, monitoreo, respaldos, restauración comprobada, incidentes |

### Hallazgos concretos del repositorio

1. `Program.cs` configura Identity y SQL Server. Los controladores comerciales revisados (`Facturas`, `Productos`, `Posventa`, `Reportes`) no muestran atributos de autorización sobre sus endpoints. **No publicar en internet sin resolverlo**. Coordinar con Russell; no sobrescribir sus cambios.
2. `Views/Shared/_LoginPartial.cshtml` todavía expone enlaces a Register/Login. Debe definirse si existen registros públicos o únicamente altas administradas; para un POS de negocio, preferir invitación/alta por administrador.
3. `Producto.Stock` se edita en el formulario; no existe en los modelos actuales una entidad de **movimiento de inventario**. La edición directa debe migrar a ajustes auditables.
4. `Pago` guarda método, monto y fecha, pero no se observa conciliación, cuenta/caja ni operaciones de reembolso.
5. `FacturasController` genera número por fecha + seis caracteres de GUID; no se observa constraint único para `NumeroFactura` en el `DbContext`. Requiere índice único, generación controlada e idempotencia.
6. La tasa del ITBIS se aplica en código como `0.18m`. Para operación real: reglas fiscales y exenciones por categoría/producto, parámetros vigentes, conservación de importes originales y pruebas.
7. `ReportesController` presenta margen sobre detalle vendido menos costo histórico; no equivale a beneficio neto o cierre de caja. Debe ajustarse para descuentos, devoluciones, gastos y pagos reales.
8. No hay proyecto de pruebas ni carpeta de workflows de CI observados en la raíz de esta rama. Faltan pruebas automatizadas y pipeline.
9. El módulo de posventa tiene estados, pero no una bitácora inmutable por transición, operador, motivo y fecha.
10. Comprobante actual es **interno no fiscal**. No se debe representar como e-CF/NCF válido sin integración y requisitos DGII.

## Prioridad previa: aislamiento multiempresa + permisos

Antes de admitir a cualquier segunda empresa, implementar y probar:
- `Empresa`, `EmpresaUsuario`, resolución de empresa autenticada en el servidor y permisos por empresa.
- `EmpresaId` obligatorio en todas las entidades comerciales, filtros globales EF, escrituras protegidas, FKs compuestas para evitar referencias entre empresas.
- Migración segura de datos actuales a `EmpresaDemo` con respaldo, conteos e integridad; pruebas A/B.
- Coordinación con Russell: Administrador/Cajero deben ser roles por empresa; el perfil de propietario de Nexora y el propietario de tienda son diferentes.
- Publicar la plataforma SaaS **solo después** de verificar autorización en cada controlador, informe, comprobante, operación de stock, PDF y trabajo automático.
- Antes del primer piloto público: plan de actualización desde .NET 8 a .NET 10 LTS. Microsoft fija el fin de soporte de .NET 8 el 10/11/2026 (https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/).

## Inicio de implementación SaaS — Fase 1A

**Primera entrega subida a `feature/nexora-ui`; pendiente de compilación y validación local:**

- `Empresa` y `EmpresaUsuario` en `Models/SaaS/`.
- Migración **aditiva** `20261021001000_CrearEmpresasYMembresias`: crea únicamente las tablas `Empresas` y `EmpresaUsuarios`, sin alterar productos, facturas ni usuarios Identity existentes.
- Clave única de slug, FK de membresía a `Empresa` y `AspNetUsers`, PK compuesta `(IdEmpresa, UserId)`, restricciones CHECK sobre estados y roles, concurrencia `rowversion`.
- Empresa nueva con estado `PendienteConfiguracion`; membresía `Suspendido` por defecto, para evitar privilegios accidentales.
- **No se activaron** pantallas multiempresa, `EmpresaId` en datos comerciales, invitaciones, alta pública ni acceso tenant-aware: toda operación continúa siendo mononegocio en esta rama y **NO es apta todavía para múltiples clientes reales**.
- Procedimiento y comprobaciones en [`docs/FASE_1A_PRUEBAS.md`](docs/FASE_1A_PRUEBAS.md). Los tests se ejecutarán desde Visual Studio y SQL Server del usuario.
- Coordinar los roles globales de Russell con la política de membresías por empresa antes de cualquier autorización o aprovisionamiento.

## Fase 1B-1 implementada en rama de desarrollo: etiquetado comercial, todavía NO aislamiento

**Pendiente de compilación y migración local** `20261021003000_PrepararDatosComercialesPorEmpresa`.

- Agrega `IdEmpresa` obligatorio con índice/FK a la empresa en `Clientes`, `Categorias`, `Productos`, `Facturas`, `DetallesFactura`, `Pagos` y `CasosPosventa`.
- Migra registros anteriores a un **Espacio inicial de compatibilidad**, ID fijo `09d17ec1-956c-4c6e-85b3-317c7cce4e01` y slug `nexora-legado` (estado `PendienteConfiguracion`). La migración **falla** si ya existe otra empresa en la base.
- Mantiene un **valor por defecto temporal** en SQL Server/EF para las operaciones comerciales aún mononegocio. **No aceptar ninguna segunda empresa**, ni usuarios externos, hasta retirar dicho default y proteger toda lectura, escritura, SQL masivo, informes y relaciones.
- Mantiene la operación de facturación existente; no crea cuentas/roles y no edita el trabajo de Russell.
- Revisión estática de 7 columnas/FK/índices y diseñador/snapshot completada; **no se ejecutó `dotnet build` ni SQL Server** en esta sesión.
- Manual de respaldo, pruebas y SQL: [`docs/FASE_1B_1_PRUEBAS.md`](docs/FASE_1B_1_PRUEBAS.md).
- **Fase 1B-2 pendiente:** autenticación + `ITenantContext`, filtro por empresa en **todos** los controladores/reportes e IDOR tests, validación de escritura, retirada de default temporal, claves foráneas compuestas, índice único por empresa y revisión de permisos Russell.
- **Fase 1C bloqueada:** no activar alta de empresas, invitaciones ni plan de suscripción antes de superar 1B-2.

## Fase 1B-2A: filtros por empresa y validación de escrituras (en desarrollo)

**Implementado en `feature/nexora-ui`, pendiente de compilación y pruebas en Visual Studio.**

- Contrato `IContextoEmpresa` y adaptador **solo local en `Development`**: identifica la empresa heredada. En cualquier otro entorno devuelve `Guid.Empty` y no permite operaciones comerciales vía `SaveChanges`.
- Contrato `IEntidadEmpresa` agregado a las siete entidades comerciales.
- `ApplicationDbContext`: filtros globales de EF Core `HasQueryFilter` para lecturas y actualizaciones directas normales por empresa, validación de alta/cambio/borrado con `SaveChanges`/`SaveChangesAsync`, sin alterar tablas Identity.
- Controladores: Clientes y Categorías dejan de actualizar modelos completos recibidos del formulario; se cargan por consulta filtrada. Consultas `FindAsync` relevantes sustituidas por LINQ. Facturas verifica que el cliente esté visible en el tenant.
- No hay nueva migración SQL para este paso: solo código de filtrado y validaciones. Regresión, consultas SQL y estados a probar en [`docs/FASE_1B_2A_PRUEBAS.md`](docs/FASE_1B_2A_PRUEBAS.md).
- **NO ES SaaS listo**: no hay contexto de tenant autenticado por membresía, todos los controladores con `[Authorize]`, FKs compuestas, retiro de default legado, tests A/B ni pruebas de SQL crudo/background jobs.
- **PELIGRO**: en `Development` el contexto legacy se concede sin revisar la identidad del usuario: debe ser SOLO local y nunca publicado. En `Production` aún no se resuelve empresa y por diseño las consultas de negocio quedan sin registros. No añadir segundas empresas.
- Integración con Russell es bloqueante para el siguiente paso `1B-2B`.

## Mejora de inventario: agotado automático y activación manual independiente

**Código integrado en `feature/nexora-ui`; pendiente de compilación y pruebas locales.**

- `Producto.Estado` sigue siendo una decisión manual; **no se cambia al llegar a cero ni al reponer**.
- Estados de inventario derivados de `Stock`: **Agotado (0)**, **Stock bajo (1–5)** y **Disponible (6+)**. Son propiedades calculadas `[NotMapped]`, por lo que **no requieren migración SQL**.
- Disponibilidad para facturar también considera `Estado` manual; un producto manualmente inactivo permanece no vendible después de reponer.
- Catálogo, ficha del producto, pantallas de edición/alta, Dashboard y Reportes muestran los estados con textos claros y sin fusionar control manual con stock.
- Se mantiene intacta la lógica transaccional de `ExecuteUpdateAsync` que evita sobregiro de inventario, oculta agotados/inactivos del selector y restituye unidades tras anular.
- Validación funcional pendiente; guía detallada en [`docs/ESTADO_INVENTARIO_AUTOMATICO_PRUEBAS.md`](docs/ESTADO_INVENTARIO_AUTOMATICO_PRUEBAS.md).
- **No se modificó Identity** ni el módulo de Russell; sigue bloqueada la activación SaaS multiempresa hasta completar la fase 1B.

## Fase SaaS 1B-2B (componente): validación segura de membresía, sin activar rutas

**Preparado en `feature/nexora-ui`, pendiente de compilación y revisión con Russell.**

- `IValidadorAccesoEmpresa`, `ValidadorAccesoEmpresaSql` y `MembresiaEmpresaValidada` permiten comprobar `Identity UserId` autenticado + empresa `Activa` + membresía `Activo` no finalizada + rol conocido.
- Consulta independiente por `Microsoft.Data.SqlClient` con parámetros para evitar dependencias circulares de `ApplicationDbContext`; el slug recibido es solo candidato y se verifica en SQL.
- **No está registrado ni conectado al pipeline MVC**: `Program.cs`, controladores e Identity de Russell sin cambios. Se mantiene `ContextoEmpresaDesarrollo` de forma provisional. **NO implica aislamiento productivo ni acceso entre empresas.**
- La empresa `nexora-legado` es `PendienteConfiguracion`, por lo que nunca se autoriza mediante este servicio hasta activar un flujo aprobado.
- Documento de revisión, pruebas negativas y condiciones de integración: [`docs/FASE_1B_2B_VERIFICACION_MEMBRESIAS.md`](docs/FASE_1B_2B_VERIFICACION_MEMBRESIAS.md).
- Próxima dependencia antes de conectar: confirmar el diseño de usuario/roles de Russell; protección por ruta con `UseAuthentication`/`[Authorize]`; filtros, FKs compuestas, eliminación de defaults, pruebas A/B.

## Prioridades: primero integridad, después automatización

### P0 — Bloqueantes antes de un piloto con operaciones reales

**P0.1 Identidad, roles y permisos (coordinar con Russell)**
- Proteger por defecto rutas de aplicación comercial; permitir solo pantallas públicas explícitas.
- Roles mínimos Administrador y Cajero; planear Supervisor/Inventario/Posventa si negocio requiere.
- Política por acción: crear/anular facturas, modificar precios, stock, pagos, cerrar caja, aprobar devoluciones.
- Alta de usuarios administrada, MFA para privilegiados cuando aplique, bloqueo y expiración de sesiones, manejo seguro de contraseñas.
- Pruebas: usuario anónimo recibe acceso denegado; cajero no puede borrar productos ni modificar permisos ni operaciones restringidas.

**P0.2 Integridad económica y de datos**
- Índices únicos SKU/código y número interno de factura; definir numeración y esquema para múltiples cajas.
- Validar cliente, descuentos, método de pago, valores no negativos, cantidad, impuestos y precios desde servidor.
- Definir ventas idempotentes: doble clic/reintento no debe cobrar ni emitir dos facturas.
- Registrar dinero recibido, diferencia y comprobación de método; cerrar caja y conciliar con dinero/banco.
- Definir inmutabilidad del documento emitido y correcciones por documentos/operaciones vinculadas.
- Conservar cálculos, fecha/hora coherente, configuraciones y versiones aplicadas en la venta.

**P0.3 Inventario trazable**
- Crear `MovimientoInventario` append-only: tipo, producto, cantidad +/- o entrada/salida, saldo, fecha, usuario, origen, referencia.
- Nunca cambiar stock por un formulario genérico sin generar movimiento; entradas por compra, ajuste autorizado y devoluciones inspeccionadas.
- Operaciones atómicas con transacciones; prueba de concurrencia y conciliación entre ledger y existencias.
- Definir productos con serial, lote y ubicación, especialmente electrodomésticos.

**P0.4 Operación y calidad**
- Pruebas unitarias y de integración con SQL Server de prueba, incluidos estados concurrentes y rollback.
- CI: restaurar, compilar, analizar, probar en PR; bloquear merges fallidos; entornos separados desarrollo/pruebas/producción.
- Secretos fuera de repositorio, backups automáticos, restore probado, registro centralizado, alertas ante errores y métricas.
- Seguridad básica: revisión OWASP, antiforgery, validación, HTTPS, headers, límites de solicitud, manejo de errores, logs sin PII sensible.

### P1 — Procesos comerciales esenciales

- **Compras y proveedores:** órdenes de compra, recepción parcial, costo de adquisición, devoluciones a proveedor.
- **Almacenes/sucursales y series:** ubicaciones, transferencias, múltiples cajas, serial IMEI/SN si corresponde.
- **Pagos reales:** múltiples formas de pago, abonos, cuentas por cobrar, cierres y arqueo, conciliación, reembolsos aprobados.
- **Posventa completa:** recepción física, inspección, reparación/reemplazo, responsable, gastos, solicitud de aprobación, notas de crédito y efectos explícitos en stock y caja.
- **Garantías documentadas:** cobertura y exclusiones, condiciones del fabricante/vendedor, certificado en español y trazabilidad por serial.
- **Informes:** reportes por vendedor/sucursal/caja, utilidad verificable, ventas netas, aging de cuentas por cobrar y exportación CSV/XLSX/PDF.
- **Catálogo:** código de barras, marca/modelo, imágenes, precios escalonados, impuestos por producto y descuentos autorizados.

### P1 Fiscal/Legal en República Dominicana (separado de una factura interna)

- Leer requisitos oficiales DGII, Ley 32-23 y documentación técnica e-CF/NCF.
- Diseñar módulo fiscal desacoplado del comprobante interno: RNC, tipos, secuencias autorizadas, XML, firma digital, acuses, estados, contingencias, notas de crédito y auditoría.
- **No emitir ni etiquetar como fiscal** hasta cumplir certificación/autorización o conectar un proveedor habilitado.
- Revisar Pro Consumidor Ley 358-05 (garantías y condiciones escritas), protección de datos y conservación documental con asesoría apropiada.
- Fuentes oficiales: https://dgii.gov.do/cicloContribuyente/facturacion/comprobantesFiscalesElectronicosE-CF/Paginas/documentacionSobreE-CF.aspx y https://www.proconsumidor.gob.do/files/Ley_General_de_Proteccin_de_los_Derechos_del_Consumidor_o_Usuario_No__358-05.pdf

### P2 — Automatización responsable

| Disparador | Automatización deseada | Salvaguarda |
| --- | --- | --- |
| Producto baja del mínimo | Alerta, sugerencia de reposición, borrador de compra | Aprobar pedido antes de enviar |
| Nueva venta | Comprobante por correo, asiento/movimiento de stock, seguimiento | Idempotencia, consentimiento/canal válido, retries |
| Caso de garantía | Asignar responsable, recordatorios de SLA, alertar retrasos | No conceder garantía ni reembolsar automáticamente |
| Pago pendiente | Recordatorios, estado cuenta actualizado | Verificar cobro real y permisos |
| Cierre diario | Resumen de ventas, caja, incidencias e inventario | Conciliar y permitir revisión |
| Error de integración | Cola reintentos, alertas y diagnóstico | Backoff e idempotencia |
| Operación crítica | Evento de auditoría y alerta a supervisor | Registro append-only y trazabilidad |

**Principio:** automatizar acciones repetibles y seguras; pedir aprobación para movimientos de dinero, ajustes sensibles o decisiones jurídicas.

### P3 — Escalamiento/Producto SaaS (solo si se valida ese mercado)

- Definir primero: **una tienda / una empresa**, múltiples sucursales de una empresa, o varias empresas independientes (SaaS).
- Para SaaS, agregar `EmpresaId/TenantId`, aislamiento estricto de datos, pruebas de fugas cross-tenant, suscripciones, provisioning y gestión de claves.
- API versionada, integraciones POS/bancos/catálogos, importación/exportación, actualización sin interrupciones, panel de soporte y feature flags.
- Diseño responsive, accesibilidad, UX de escáner/impresora, búsquedas rápidas y uso eficiente de red.

## Orden de ejecución propuesto

| Entrega | Alcance | Criterio para pasar |
| --- | --- | --- |
| 0. Baseline | Confirmar rama, migraciones, compilar, tests de humo, respaldo | Instalación reproducible sin errores |
| 1. Seguridad | Integrar roles Russell, restringir endpoints, cuentas | Todas las rutas y acciones sensibles tienen pruebas de autorización |
| 2. Inventario | Modelo de movimientos, prohibir cambio directo silencioso | Stock conciliable y trazable por usuario/origen |
| 3. Facturación/pagos | Idempotencia, numeración, caja, parcialidades, cierre | Sin duplicados, reportes concilian y rollback probado |
| 4. Posventa y fiscal | Reembolso explícito, nota de crédito, garantías, DGII según objetivo | Conformidad y trazabilidad validadas |
| 5. Producción piloto | Tests CI, monitoreo, backups, despliegue | Prueba de restauración + pruebas E2E + piloto con datos controlados |
| 6. Automatización | Alertas y tareas programadas seguras | Medición de falsas alarmas, fallos y autorización |

## Política de desarrollo y definición de terminado

Toda funcionalidad debe incluir:
1. Requisito comercial y casos límite escritos.
2. Permisos del usuario y política de datos.
3. Modelo relacional, restricciones e integridad histórica; migración reversible.
4. Validación servidor, concurrencia, idempotencia si hay dinero o stock.
5. Interfaz con errores claros y responsive.
6. Pruebas unitarias, integración y escenarios de usuario.
7. Logs/auditoría, backups o recuperación si corresponden.
8. Documentación de operación y regresión de módulos relacionados.
9. Revisión de código y ejecución real antes de fusionar PR.

## Decisiones pendientes del equipo

- **Modelo de negocio inicial (DECIDIDO):** SaaS multiempresa. Base SQL Server compartida y aislamiento por `EmpresaId` para MVP, con diseño para escalar a bases aisladas si es necesario.
- **Primer sector piloto:** tienda de electrodomésticos, PyME comercial general, o mixto.
- **Facturación fiscal:** desarrollo propio DGII, proveedor certificado o piloto interno sin emisión fiscal.
- **Canal de despliegue:** PC local, LAN de negocio, web en nube, o híbrido.
- **Integración Russell:** rama, calendario, roles efectivos y revisión de controladores/Identity.
- **Política de POS/stock/caja:** quién puede cambiar cantidades, aprobar devoluciones, registrar reembolsos y cerrar turnos.

> Este plan parte de la **inspección de archivos en GitHub**; no equivale a auditoría completa de seguridad, cumplimiento ni a la ejecución de tests. No usar datos reales ni exponer Nexora públicamente antes de cerrar los P0.

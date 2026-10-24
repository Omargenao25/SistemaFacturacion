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

## Fase SaaS 1B-2C: integridad relacional por empresa (migración pendiente de pruebas)

**Preparada en `feature/nexora-ui`; no compilada ni aplicada en SQL Server por el asistente.**

- `ApplicationDbContext` protege 6 referencias mediante FKs compuestas `(IdEmpresa, IdRegistro)`: Producto–Categoría, Factura–Cliente, Detalle–Factura, Detalle–Producto, Pago–Factura y Posventa–Detalle.
- Migración `20261021175000_AislarRelacionesComerciales`: valida 6 tipos de referencias antes de alterar; si detecta cruce de empresa, **aborta**. Sustituye 6 FKs individuales por las compuestas, crea 5 claves alternas y 6 índices compuestos, todos `NoAction` y sin borrar datos.
- `Down()` recupera el esquema anterior y las FKs previas; nueva metadata EF (`Designer.cs`) y `ApplicationDbContextModelSnapshot.cs` revisadas estáticamente.
- **La migración no se ejecuta automáticamente**. El usuario debe respaldar y comprobar SQL antes de `Update-Database`.
- Checklist y consultas: [`docs/FASE_1B_2C_RELACIONES_COMERCIALES.md`](docs/FASE_1B_2C_RELACIONES_COMERCIALES.md).
- **No crea empresas ni usuarios**; mantiene default de legado y el verificador de membresías aún desconectado. No permitir segundo tenant, publicar ni fusionar PR hasta integrar Identity, roles y pruebas A/B.

## Módulo financiero inicial: solicitudes de reembolso al anular (código preparado)

**En `feature/nexora-ui`; pendiente de compilación, migración y pruebas SQL locales.**

- Nueva entidad `Reembolso` por factura y empresa. FK compuesta e índice único impiden crear dos solicitudes para la misma factura o relacionarlas con otra empresa.
- La anulación transaccional que ya restauraba stock también crea `Reembolso` en estado **Pendiente** cuando pagos y factura coinciden. Discrepancias/sin pagos/métodos mixtos → **PendienteRevision**.
- El importe surge de la suma de `Pago.Monto` existente y se conserva el método original: no es igual necesariamente al total de venta.
- No hay devolución monetaria automática, notas de crédito ni endpoint de aprobación; la confirmación requiere Identity + permisos de administrador por empresa que Russell está desarrollando.
- `Details` de factura anulada muestra importe, estado y método, sin atribuir reembolso a anulaciones históricas que carezcan del nuevo registro.
- Migración `20261021193000_CrearReembolsosPendientes`; verificar compilación, respaldo, `Update-Database` y regresión antes de aceptar el hito.
- Guía: [`docs/REEMBOLSOS_PENDIENTES_PRUEBAS.md`](docs/REEMBOLSOS_PENDIENTES_PRUEBAS.md).
- **Deuda de seguridad/operación importante**: sigue existiendo el reintegro automático de stock sin prueba física de devolución. Se debe separar recepción de mercancía/anulación y preparar caja, conciliación y devoluciones parciales antes de uso comercial real.

## Recepciones de mercancía por anulación — fase inicial preparada

**Pendiente de `dotnet build`, migración y pruebas reales en desarrollo.**

- `RecepcionAnulacion`: una obligación de recepción física por detalle de factura, `CantidadEsperada`, `CantidadRecibida = 0`, `CantidadLiberada = 0`, estado `Pendiente`, auditoría preparada; FK compuesta y unicidad por empresa + detalle.
- `FacturasController.Anular` deja de restaurar automáticamente las existencias vendibles: crea recepciones pendientes y reembolso pendiente en la **misma transacción**. La factura sigue quedando Anulada, con protección de 7 días y no duplicación.
- Nuevo panel de mercancía pendiente en `Details` y modal actualizado: ninguna unidad se considera recibida ni puesta en venta automáticamente.
- Migración `20261021203000_RegistrarRecepcionesPendientesAnulacion`: tabla nueva solamente, sin backfill ni alteración del stock histórico. Las anulaciones anteriores conservan sus datos y muestran aviso de conciliación histórica.
- **Deliberadamente no se habilita confirmar recepción ni liberar stock** hasta contar con roles de Russell, una política de inspección física y registro idempotente de movimientos. Queda bloqueado el uso comercial real.
- Guía y consultas: [`docs/RECEPCIONES_ANULACION_PRUEBAS.md`](docs/RECEPCIONES_ANULACION_PRUEBAS.md). Respaldar, compilar, aplicar migración en Development y probar emisión-anulación con nueva expectativa: stock NO se restaura.

## Inspección y clasificación de mercancía — lectura y simulación (preparada)

- Nueva bandeja `GET /Recepciones` y ficha `GET /Recepciones/Details/{id}`, enlazadas desde menú lateral y detalle de factura anulada.
- Clasificación **simulada** en apta para venta / cuarentena / reparación / descarte. Reglas puras verifican cantidades no negativas, sumas y límites; resultados calculados por servidor a través de `GET` sin efectos de persistencia.
- `RecepcionesController` **solo tiene acciones GET**; no se modifica stock, recepción, factura, pago o reembolso. No se añade endpoint de confirmar/guardar hasta contar con los permisos de Russell.
- Nuevos archivos `Services/Inventario/ReglasInspeccionMercancia.cs`, `ViewModels/InspeccionRecepcionViewModel.cs`, `Controllers/RecepcionesController.cs`, `Views/Recepciones/Index.cshtml` y `Views/Recepciones/Details.cshtml`. Estilos responsive en `site.css`.
- **No requiere migración SQL nueva.** Compilación y pruebas de navegación/simulación en la máquina del usuario pendientes de confirmar.
- Guía: [`docs/INSPECCION_MERCANCIA_FASE_PREPARACION.md`](docs/INSPECCION_MERCANCIA_FASE_PREPARACION.md).
- No operar en público con el contexto de desarrollo heredado; no confirmar inspecciones, liberar stock ni devolver dinero antes de Identity, autorización tenant y auditoría.

## Centro de control de anulaciones (lectura operativa preparada)

- Nueva ruta `GET /ControlAnulaciones`: panel por factura **Anulada**, con saldo de solicitud financiera, estado de recepción física, datos faltantes y acceso al detalle comercial.
- Búsqueda por factura/cliente y paginación SQL de 25 facturas. Indicadores expresamente **limitados a la página visible**, sin multiplicar reembolsos por número de productos.
- Detecta anulaciones antiguas sin solicitud/recepción y líneas de recepción incompletas como pendientes de conciliación, sin fabricar comprobantes ni movimientos históricos.
- Archivos `Controllers/ControlAnulacionesController.cs`, `ViewModels/ControlAnulacionesViewModel.cs`, `Views/ControlAnulaciones/Index.cshtml`, menú y CSS.
- **Solo GET, sin migración ni escrituras**. La autorización global y por empresa de Russell sigue pendiente. Guía: [`docs/CENTRO_CONTROL_ANULACIONES_PRUEBAS.md`](docs/CENTRO_CONTROL_ANULACIONES_PRUEBAS.md).
- Implementación y revisión estática en GitHub: pendiente `Ctrl+Shift+B` y comprobación visual/funcional local. No desplegar públicamente ni fusionar PR.

## Seguridad empresarial — fase 1 preparada para revisión

**Rama independiente:** `feature/nexora-security-phase1` (no mezclada aún con `feature/nexora-ui`). Esta fase **no activa SaaS real** ni garantiza protección granular sobre todos los controladores comerciales.

- `Program.cs`: `UseAuthentication()` explícito, registro de políticas de seguridad y middleware de validación **solo con opt-in** `Seguridad:ActivarTenancyValidada`. En Production se requiere autenticación en rutas MVC convencionales y por atributos.
- `ContextoEmpresaVerificado` scoped, `VerificacionMembresiaMiddleware`: `Guid.Empty` inicial; membresía y rol establecidos solo tras verificación SQL del usuario Identity y slug de ruta. Se conserva modo Development legado por defecto para no romper pruebas existentes.
- `PermisosNexora`, `PermisoEmpresaRequirement` y `PermisoEmpresaHandler`: matriz de permisos por rol empresarial, independiente de los roles globales de Identity.
- Prueba protegida `GET /e/{empresaSlug}/seguridad/estado`: solo devuelve el tenant/rol verificado y no escribe datos.
- Runner `tests/Nexora.Security.Checks` sin dependencias NuGet de prueba adicionales (12 verificaciones de roles, auth, contexto, errores).
- **Sin nuevas migraciones, sin POST sensibles, sin habilitar reembolsos o recepción de stock.** Compilación y tests locales pendientes.
- Guía: [`docs/SEGURIDAD_FASE_1_IDENTIDAD_MEMBRESIAS.md`](docs/SEGURIDAD_FASE_1_IDENTIDAD_MEMBRESIAS.md).
- Próximo bloqueante: autenticación + autorización por operación en cada controlador real, pruebas cruzadas A/B, auditoría, y alta/invitación de usuarios antes de producción.

## Seguridad empresarial — fase 2 preparada (enforcement opt-in)

**Rama nueva:** `feature/nexora-security-phase2`, basada en `feature/nexora-security-phase1`. **Pendiente de compilar y probar con Identity/SQL reales; no activada por defecto.**

- `Security/ReglasAccesoControladores.cs`: matriz cerrada de `(controlador, acción MVC, método HTTP) -> policy`, denegación por defecto. Se cubren Facturas, Clientes, Productos, Categorías, Posventa, Recepciones, Control de Anulaciones, Reportes y Home. Las rutas nuevas no obtienen privilegios automáticamente.
- `Security/FiltroPermisosComerciales.cs`: `IAsyncAuthorizationFilter` que evalúa el permiso en el servidor a través de `IAuthorizationService` y el `ContextoEmpresaVerificado` scoped (de la fase 1).
- `Program.cs`: instala filtro y ruta `e/{empresaSlug}/{controller}/{action}/{id?}` **solo si** `Seguridad:ActivarTenancyValidada=true`; en Development usual permanece OFF sin cambios comerciales. Si ON, las rutas legacy sin slug carecen de contexto empresarial y son denegadas.
- `Security/PermisosNexora.cs`: añade `AdministrarClientes`, `ConsultarRecepciones`, `RegistrarPosventa`, `ResolverPosventa`. Cajero puede consultar/registrar posventa y crear facturas, pero no anular, liberar stock ni confirmar devoluciones.
- Runner `tests/Nexora.Security.Checks`: 23 verificaciones programadas (sin dependencias nuevas); falta correr en Visual Studio.
- **Sin migración, sin nuevos POST que muevan dinero o mercancía**, sin modificar controladores comerciales ni datos previos.
- Documento: [`docs/SEGURIDAD_FASE_2_AUTORIZACION_COMERCIAL.md`](docs/SEGURIDAD_FASE_2_AUTORIZACION_COMERCIAL.md).
- Pendiente: navegación con slug, selector de empresa, Identity/roles y usuarios, pruebas negativas A/B con SQL real, auditoría y revisión de permisos por entidad. Mantener ramas y PR en borrador, sin activar SaaS público.

## Seguridad empresarial — fase 3: directorio Mi equipo (solo lectura)

**Rama:** `feature/nexora-security-phase3-team` encima de fase 2. Pendiente de compilación y pruebas SQL/HTTP en laboratorio. **No apta para producción.**

- `Controllers/EquipoController.cs`: nuevo GET protegido por `[Authorize(Policy = AdministrarUsuarios)]`, comprobaciones redundantes de empresa Activa y permiso Propietario, y filtro **explícito** `EmpresaUsuarios.IdEmpresa == ContextoEmpresaVerificado.IdEmpresa`. La tabla de membresías es global; nunca se consulta para todo el sistema.
- `ViewModels/EquipoEmpresaViewModel.cs` y `Views/Equipo/Index.cshtml`: directorio de empleados, estado de membresía, rol y correo confirmado, filtros y paginación de 20. Indicadores por empresa y matriz derivada del código `PermisosNexora`.
- `Security/ReglasAccesoControladores.cs`: `Equipo.Index.GET` requiere `AdministrarUsuarios`; se deniegan por defecto los POST y acciones desconocidas.
- `Views/Shared/_Layout.cshtml`: menú **Mi equipo** visible únicamente cuando el usuario tiene membresía verificada y rol que concede este permiso; no aparece en Development legado (flag apagada).
- Runner `tests/Nexora.Security.Checks`: **28 casos definidos**, nuevos casos sobre autorización de propietario, denegación a administrador/cajero y ausencia de edición.
- No habilita invitaciones, cambios de roles, altas, bajas ni escrituras de Identity/EmpresaUsuario. Requiere auditoría, CSRF, RowVersion y protección del último propietario para esas funciones posteriores.
- Sin migraciones SQL. Guía: [`docs/SEGURIDAD_FASE_3_EQUIPO_SOLO_LECTURA.md`](docs/SEGURIDAD_FASE_3_EQUIPO_SOLO_LECTURA.md).
- No activar `Seguridad:ActivarTenancyValidada` en tu base normal, no habilitar tenants nuevos ni fusionar los PR en `master`.

## Seguridad empresarial — fase 4: membresías auditadas e invitaciones (laboratorio)

**Rama:** `feature/nexora-security-phase4-members`, basada en fase 3. **Requiere compilación, migración y pruebas de integración SQL; no apta para producción.**

- `GestionMembresiasSegura`: transacciones SERIALIZABLE parametrizadas sobre tablas globales, revalidación del propietario, RowVersion, protección de cuentas Propietario, cambios de rol `Cajero/Administrador`, suspensión/reactivación y auditoría atómica.
- Invitaciones para `Cajero/Administrador` con token aleatorio 32 bytes, hash SHA-256, un uso y expiración 48 horas. Entrega **manual** del link. Aceptación con Identity y EmailConfirmed coincidente, empresa Activa, invitante vigente y sin membresía duplicada. No existe todavía envío automático de correos.
- `EquipoController` y Razor: POST con antiforgery para rol, estado e invitación; bitácora reciente de 20 movimientos por tenant. `InvitacionesController` acepta desde fuera del contexto comercial mediante excepción muy restringida y atributo [Authorize].
- Migración `EquipoSeguridadAuditoriaInvitaciones`: tablas `InvitacionesEmpresa` y `AuditoriaMembresias` sin cambiar las entidades comerciales ni snapshot EF. **Respaldar y aplicar solo en base de laboratorio después de compilar**. Rollback destructivo de auditoría bloqueado.
- Runner `tests/Nexora.Security.Checks`: **38 verificaciones programadas**, pendientes de ejecución en Visual Studio. Sin confirmaciones financieras ni devoluciones reales.
- Guía: [`docs/SEGURIDAD_FASE_4_GESTION_EMPLEADOS.md`](docs/SEGURIDAD_FASE_4_GESTION_EMPLEADOS.md).
- Todavía falta revocación de invitaciones, proveedor de correo + confirmación Identity, propagación completa de rutas SaaS, SQL integration A/B, y hardening antes de producción. No activar tenants en base usual ni fusionar a master.

## Seguridad empresarial — fase 5: revocación de invitaciones (laboratorio)

**Rama:** `feature/nexora-security-phase5-invites`, basada en fase 4. **No compilada ni probada con SQL Server desde el asistente.**

- `GET /e/{empresaSlug}/Equipo/Invitaciones`: bandeja paginada por empresa, filtros de pendientes vigentes, vencidas, aceptadas, revocadas y todas. No muestra token ni hash.
- `POST /e/{empresaSlug}/Equipo/RevocarInvitacion`: propietario autenticado, anti-CSRF, verificación SQL de pertenencia y permiso con transacción Serializable; invalida solo enlace `Pendiente` y vigente, y audita atómicamente `InvitacionRevocada`.
- La revocación solo invalida un enlace aún no aceptado, **no cambia membresías ya activas**. La aceptación previa rechaza token cuyo estado no sea `Pendiente`.
- Migración SQL `20261024143000_RegistrarRevocacionInvitaciones`: amplía check constraint de auditoría para la nueva acción. **Aplicar solamente en BD aislada después de la migración de fase 4**, con backup, compilación y tests previos.
- Pruebas unitarias preparadas: **45** en total; integración A/B, idempotencia y concurrencia de revocación requieren verificación local.
- Guía: [`docs/SEGURIDAD_FASE_5_REVOCACION_INVITACIONES.md`](docs/SEGURIDAD_FASE_5_REVOCACION_INVITACIONES.md).
- El modo SaaS validado sigue OFF por defecto en Development habitual; no desplegar públicamente ni fusionar a master.

## Seguridad empresarial — fase 6: selector y navegación con empresa verificada

**Rama:** `feature/nexora-security-phase6-company-selector` sobre fase 5. Fuente subida; compilación y pruebas HTTP/SQL pendientes de confirmar localmente.

- `GET /mis-empresas` muestra **solo** empresas Activa en las que el usuario Identity tenga membresía Activo sin baja. Consulta global `EmpresaUsuarios` restringida explícitamente al UserId autenticado; sin alta de empresas ni selección automática.
- `/e/{empresaSlug}/...` sigue validándose contra SQL y políticas por petición. El selector no almacena ni concede autorización empresarial; la URL contiene únicamente un slug candidato.
- Modo SaaS OFF por defecto: funcionamiento normal del Development legado sin cambios. ON solo en laboratorio: `/` lleva al selector, menú comercial se oculta hasta verificar empresa, links principales preservan slug y se muestran según permisos del rol.
- Validador SQL y selector comparten reglas de slug `SlugEmpresaSeguro`. `EmpresasAcceso.Index.GET` solo se exceptúa del filtro comercial para un Identity autenticado; no hay POST ni nuevos permisos administrativos.
- **Sin migración de esta fase ni escrituras a datos comerciales.** Suite de verificación preparada: **53** casos. Aplicar migraciones pendientes de fases 4–5 **solo en base de laboratorio aislada** antes de activar multiempresa; no tocar base normal.
- Guía: [`docs/SEGURIDAD_FASE_6_SELECTOR_EMPRESAS.md`](docs/SEGURIDAD_FASE_6_SELECTOR_EMPRESAS.md).
- Pendientes: rutas y redirecciones de TODAS las vistas comerciales, flujo de onboarding, Identity email y pruebas HTTP/SQL entre empresas antes de publicar.

## Seguridad empresarial — fase 7: integración SQL real con dos tenants ficticios

**Rama:** `feature/nexora-security-phase7-sql-lab`, basada en la fase 6. *Suite implementada pero todavía NO ejecutada en SQL Server ni compilada por el asistente*.

- Nuevo runner `tests/Nexora.Integration.Lab`: **21 casos SQL + backend** con dos empresas y dos usuarios Identity ficticios, validación real de membresía, middleware, permisos, query-filters EF, bloqueos de escritura cruzada, suspensión y baja.
- Sólo admite **SQL Server local + base exacta `NexoraIsolationTest` + marca manual `NexoraIsolationLabGuard`**, y se niega a escribir si hay datos comerciales o usuarios anteriores. No utiliza `DefaultConnection`, no ejecuta migraciones y limpia sus datos falsos en `finally`.
- Se mantiene runner anterior de **53 comprobaciones lógicas**. Objetivo combinado **74**; falta verificarlo de verdad en Visual Studio. **No son pruebas HTTP con cookie real ni publicación SaaS aprobada**.
- `tests/Nexora.Integration.Lab/VALIDAR_LAB.sql` crea marca de autorización solo si ya estás en la base de laboratorio correcta.
- Sin cambios en controladores comerciales ni migraciones de esta fase. Documento: [`docs/SEGURIDAD_FASE_7_PRUEBAS_SQL_AISLAMIENTO.md`](docs/SEGURIDAD_FASE_7_PRUEBAS_SQL_AISLAMIENTO.md).
- Flags comerciales siguen OFF; no probar dos tenants en base normal ni publicar la plataforma. Después: navegación real HTTP/cookies, formularios, rutas y concurrencia.

## Seguridad empresarial — fase 8: navegación y pruebas de login reales (laboratorio)

**Rama:** `feature/nexora-security-phase8-navigation`, basada en fase 7. Revisión estática satisfactoria; **falta compilación y prueba en el navegador del usuario**.

- `PreservarRutaEmpresaFiltro`, solo cuando `Seguridad:ActivarTenancyValidada=true`: preserva slug empresarial validado en `RedirectToAction` y evita reenvíos a rutas comerciales sin empresa.
- Enlaces y formularios de clientes, categorías, productos, facturas, posventa, recepciones, anulaciones y reportes incluyen la empresa actual explícitamente (si existe); el modo Development legado queda igual.
- Runner lógico: **58 casos preparados** (5 pruebas nuevas de rutas, redirecciones y denegación financiera). Las 21 pruebas SQL anteriores permanecen independientes.
- `tests/Nexora.Browser.Lab`: genera cuatro cuentas temporales con hash real de ASP.NET Identity y contraseñas aleatorias para **dos empresas ficticias** con roles Propietario/Cajero y una identidad sin membresía. Solo acepta SQL local, DB `NexoraIsolationTest` y marca de guardia explícita; se niega a utilizar base con datos previos y dispone de `--cleanup` con validación exacta de fixture.
- `docs/SEGURIDAD_FASE_8_NAVEGADOR_IDENTIDAD.md`: guía para pruebas reales de login, cookie, selector, navegación por roles y acceso cruzado. El programa de navegación sigue requiriendo comprobación manual de HTTP, no simular resultados.
- Sin migraciones nuevas ni cambios a facturación/stock/dinero. **No activar modo validado en la base habitual**, ni fusionar a master ni desplegar público.

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

# NEXORA SaaS — Arquitectura multiempresa (ADR-001)

**Fecha de decisión:** 2026-10-19  
**Estado:** DISEÑO APROBADO COMO DIRECCIÓN DE PRODUCTO; todavía NO implementado.  
**Rama documental:** `feature/nexora-ui` (PR #1 en borrador).  
**Decisión de producto:** plataforma SaaS en la que varias empresas independientes contratarán Nexora con usuarios, catálogos, clientes, inventarios, facturas, pagos, garantías y posventa **estrictamente separados**.

> **Advertencia de seguridad:** el código actual es mononegocio y todavía presenta endpoints comerciales sin autorización. NO activar registro de nuevas empresas, acceso desde internet o carga de datos reales hasta haber implementado, probado y auditado el aislamiento multiempresa. La documentación **no es una implementación**.

## 1. Decisiones arquitectónicas (por qué)

1. **Monolito modular ASP.NET Core + SQL Server**, antes que microservicios: transacciones de ventas/inventarios/pagos y despliegue son más sencillos, con límites de módulo preparados para extraer servicios si crece la demanda.
2. **Una base SQL Server, esquema compartido y columna `EmpresaId`** para un MVP: menor coste y complejidad operativa. Diseñar una interfaz de resolución de datos que permita, en el futuro, una base por empresa de mayor tamaño si los requisitos lo justifican. NO mezclar bases por empresa sin automatizar migraciones, respaldos y seguridad.
3. **Una identidad global por persona (`AspNetUsers`) y membresías por empresa**. Un mismo correo puede administrar la empresa A y ser cajero de B, con roles independientes. Los roles `Administrador` y `Cajero` que prepara Russell se mapearán a permisos por membresía, no se reutilizarán como roles globales para todas las empresas.
4. **Contexto de empresa por solicitud**, verificado en el servidor después de autenticar. El `slug` en la ruta/subdominio solo identifica la empresa solicitada, **no autoriza** a acceder a ella.
5. **Fail closed**: sin empresa autenticada y activa, sin membresía activa o con empresa suspendida, denegar operaciones de negocio. No usar `EmpresaId` enviado en HTML, JSON, query string o formulario como fuente de autorización.
6. **Control de acceso a nivel de base de datos y aplicación**: filtros globales EF + comprobación explícita en escrituras y `ExecuteUpdateAsync` + índices únicos por empresa + claves foráneas compuestas por `EmpresaId` para impedir relaciones cruzadas. Evaluar SQL Server Row-Level Security como defensa adicional antes de producción, con pruebas de conexión/contexto.
7. **Auditoría y eventos inmutables** para operaciones monetarias, acceso administrativo, cambio de empresa, inventario, anulación, reembolsos, cambios de roles y suscripciones. Automatizar tareas repetitivas con idempotencia y aprobación cuando hay efectos financieros/legales.
8. **Separar facturación del comercio y facturación de la plataforma**. `Pago` / `Factura` de una tienda no es el cobro de la suscripción SaaS. Las suscripciones viven en el plano de plataforma.

## 2. Módulos y responsabilidades

### Plano de plataforma — no pertenece a ninguna tienda específica

- **Identidad**: usuarios y autenticación ASP.NET Identity (Russell).
- **Gestión de empresas**: alta, validación, estado, dueño, selección de espacio de trabajo.
- **Membresías/roles/permisos**: pertenencia usuario↔empresa, invitaciones, altas y bajas; políticas por acción.
- **Planes y suscripciones**: plan, cuota, límite de usuarios/sucursales/recursos, prueba, renovación, suspensiones, proveedor de cobros (futuro).
- **Operaciones SaaS**: monitoreo, auditoría de plataforma, aprovisionamiento, versiones, migraciones, respaldos por seguridad.
- **Soporte de plataforma**: acceso excepcional temporal, con justificación, registro y consentimiento cuando corresponda; jamás privilegios invisibles.

### Plano de datos de cada empresa — `EmpresaId` obligatorio

- Clientes, categorías, productos, proveedores, sucursales/almacenes.
- Facturas, detalles, pagos comerciales, caja, conciliaciones, notas de crédito.
- Movimientos de inventario, series/lotes y ajustes.
- Garantías registradas, casos de posventa, devoluciones y reparaciones.
- Informes, adjuntos, preferencias fiscales, plantillas y actividades de usuarios.
- Tareas, correos y notificaciones emitidos por cuenta de la empresa.

## 3. Modelo de entidades y relaciones

### Nuevas entidades de plataforma

| Entidad | Campos esenciales | Restricciones |
| --- | --- | --- |
| `Empresa` | `IdEmpresa: Guid`, `Slug`, `NombreComercial`, `RNC` (opcional), `Estado`, `ZonaHoraria`, `Moneda`, `FechaCreacionUtc` | `SlugNormalizado` globalmente único, `Estado` validado; RNC sujeto a reglas locales |
| `EmpresaUsuario` | `IdEmpresa`, `UserId`, `Rol`, `Estado`, `FechaAltaUtc`, `FechaBajaUtc` | PK compuesta `(IdEmpresa, UserId)`; FK a `AspNetUsers` y `Empresa` |
| `InvitacionEmpresa` | `Id`, `IdEmpresa`, `EmailNormalizado`, `Rol`, `TokenHash`, `VenceUtc`, `UsadoUtc` | Nunca almacenar el token en claro; uso único, vencimiento, caducidad y revocación |
| `AuditoriaPlataforma` | `Id`, `ActorId`, `IdEmpresa` opcional, `Accion`, `FechaUtc`, `CorrelacionId`, `DatosSeguros` | Append-only; datos personales minimizados |
| `Plan`/`Suscripcion` (fase posterior) | `IdEmpresa`, tipo, límites, periodo, estado, proveedor/ref externa | Índices e idempotencia de webhooks; NO activar cobros antes de validación |

### Cambios obligatorios en las entidades comerciales existentes

Añadir `IdEmpresa: Guid` a:

`Cliente`, `Categoria`, `Producto`, `Factura`, `DetalleFactura`, `Pago`, `CasoPosventa`.

Futuras entidades (ej. `MovimientoInventario`) incorporan `IdEmpresa` desde el primer día. Las tablas de Identity son globales **pero no conceden** permisos comerciales por sí solas.

**Integridad crítica:** mantener IDs internos actuales para facilitar migración, pero incorporar claves alternas / índices únicos `(IdEmpresa, IdCliente)`, `(IdEmpresa, IdProducto)`, etc., y FKs compuestas:

- `Factura(IdEmpresa, IdCliente) → Cliente(IdEmpresa, IdCliente)`.
- `Producto(IdEmpresa, IdCategoria) → Categoria(IdEmpresa, IdCategoria)`.
- `DetalleFactura(IdEmpresa, IdFactura) → Factura(IdEmpresa, IdFactura)`.
- `DetalleFactura(IdEmpresa, IdProducto) → Producto(IdEmpresa, IdProducto)`.
- `Pago(IdEmpresa, IdFactura) → Factura(IdEmpresa, IdFactura)`.
- `CasoPosventa(IdEmpresa, IdDetalle) → DetalleFactura(IdEmpresa, IdDetalle)`.

Así, una factura de A **no puede** referenciar el producto de B aunque el ID numérico se manipule. Mantener sin eliminación en cascada las asociaciones que protegen el historial.

### Unicidad por empresa (no global)

- `(IdEmpresa, Codigo)` único para SKU de productos.
- `(IdEmpresa, NumeroFactura)` único para comprobantes internos.
- `(IdEmpresa, IdSucursal, NombreAlmacen)` según futura estructura.
- El `SlugNormalizado` de Empresa sí es **único global**.

## 4. Resolución del contexto de empresa y control de acceso

**Primer diseño de URL recomendado:** `/e/{slug}/...` para desarrollo (sin complicar DNS/subdominios). Más adelante `{slug}.nexora.com`, si se implementa resolución segura del host y protección frente a host-header spoofing. El inicio de sesión puede ser global, seguido de un selector de empresa.

**Flujo de una petición comercial:**

1. ASP.NET autentica al usuario (Identity).
2. Middleware o filtro de tenant resuelve el slug y carga la empresa.
3. Comprueba empresa **Activa** y `EmpresaUsuario` **Activo** del usuario autenticado; obtiene el `IdEmpresa` en un servicio `ITenantContext` scoped, **solo lectura para controladores**.
4. Comprueba permiso de la acción: p. ej. `Ventas.Emitir`, `Ventas.Anular`, `Inventario.Ajustar`, `Posventa.Cerrar`.
5. Contexto EF obtiene `IdEmpresa` de ese servicio scoped para filtrar **todas** las consultas comerciales.
6. `SaveChanges`/interceptor valida y asigna `IdEmpresa` desde el servidor, rechaza entidades con tenant diferente, y eventos registran actor/empresa/origen.
7. Índices y FKs compuestas validan en SQL Server las relaciones y unicidades.
8. Trabajos en segundo plano reciben tenant verificado explícitamente y una identidad de servicio; **nunca** comparten un `DbContext` entre empresas.

**No asumir que un usuario que conoce el slug o ID puede ver datos**. Validar pertenencia por petición (o mecanismo de caché de membresía con revocación inmediata). Evitar confiar en un valor de sesión no verificado. Para un usuario en varias empresas, cambiar de espacio de trabajo debe revalidar permisos.

### EF Core, rutas y consultas

- En `ApplicationDbContext`, filtros `HasQueryFilter(e => e.IdEmpresa == tenantContext.IdEmpresa)` para **cada** entidad de negocio; con EF8, si se combinan con borrado lógico, usar una sola expresión `&&`.
- Complementar con políticas/proveedores que impidan `IgnoreQueryFilters` no autorizado, `FromSqlRaw`, `ExecuteDeleteAsync` y consultas `ExecuteUpdateAsync` sin `EmpresaId`.
- `FindAsync(id)` y mutaciones directas deben obtener el registro con el tenant explícitamente: `SingleOrDefaultAsync(x => x.IdEmpresa == tenantId && x.Id == id)`; jamás vincular por ID sin comprobar la empresa.
- Restringir `EmpresaId` del model binding: generarlo/validarlo exclusivamente en el servidor.
- Trabajos de importación, reportes, PDF, exportaciones, correo y cache usan claves tenant-aware; cache keys `{EmpresaId}:{Recurso}:{Id}`.
- Evaluar y probar SQL Server Row-Level Security antes de producción como defensa adicional, sin usar el mismo rol de BD para el plano global y el plano comercial.

## 5. Autorización: acuerdo de trabajo con Russell

En Nexora actual, `Administrador` y `Cajero` no deben actuar como permisos globales en el SaaS. El **rol está en `EmpresaUsuario`**, y las políticas se resuelven en función de la empresa activa.

| Permiso | Propietario/Administrador empresa | Cajero |
| --- | --- | --- |
| Crear/consultar clientes de su empresa | Sí | Sí, si su operación lo requiere |
| Emitir factura y registrar cobro | Sí | Sí |
| Consultar catálogo e inventario | Sí | Sí |
| Ajustar stock/precio/costo | Sí, con auditoría | No por defecto |
| Anular factura dentro de política | Sí, según autorización | Solo con aprobación si se habilita |
| Cerrar/autorizar reembolso posventa | Sí, con auditoría | No |
| Invitar usuarios y asignar roles | Sí | No |
| Cambiar plan y datos de facturación SaaS | Propietario | No |
| Ver datos de otra empresa | Nunca, salvo su membresía independiente | Nunca |

El usuario **Propietario** puede mapearse a una membresía con permisos administrativos más `Empresa.AdministrarMiembros`; coordinar nomenclatura/implementación con Russell. Los operadores internos de Nexora constituyen identidad y roles de **plataforma**, separados de las membresías de empresas.

## 6. Seguridad e integridad multiempresa (obligatorio)

- Autenticación y autorización **antes de consultas de negocio**; `[Authorize]` o política global de fallback con excepciones públicas explícitas.
- Exigir permisos en GET/POST, endpoints, informes, búsqueda, acceso a archivos, PDF, exportaciones, workers y acceso a API.
- Protección antiforgery en operaciones HTML, sesiones y cookies seguras, expiración/revocación de permisos, MFA para roles privilegiados cuando esté disponible.
- `TenantId` no es argumento de autorización; los recursos cruzados devuelven 404/403 sin revelar contenido ni pertenencia.
- Pruebas IDOR: usuario A intenta `/e/b/Facturas/Details/123`, POST manual de factura/cliente de B, exportación de B, filtro de B, `ExecuteUpdate` de B; **no hay lectura ni escritura**.
- Auditoría de intentos de acceso indebido y operaciones sensibles.
- Integridad y fiscalidad: documentos financieros inmutables, corrección por operación compensatoria y cumplimiento dominicano (módulo DGII futuro).

## 7. Migración segura del sistema que hoy funciona

**No ejecutar automáticamente una migración destructiva ni habilitar dos empresas antes de completar esta lista.**

| Etapa | Migración/entrega | Puerta de salida |
| --- | --- | --- |
| A. Base verificada | Respaldar/restaurar BD local; integrar rol Russell en rama acordada; compilar, ejecutar migraciones actuales y pruebas de humo | Sin errores, respaldo restaurable |
| B. Tablas nuevas (aditivas) | `Empresa`, `EmpresaUsuario`, opcional `InvitacionEmpresa` con constraints; **sin tocar datos de clientes/productos/facturas** | EF migraciones con rollback probado, solo plataforma y admin sin acceso cross-tenant |
| C. Adopción de datos heredados | Empresa de desarrollo `EmpresaDemo` + membresía del usuario dueño; añadir `EmpresaId` temporalmente nullable en todas las tablas comerciales y asignar registros existentes de forma explícita a `EmpresaDemo` | Conteos y FK iguales antes y después; sin huérfanos |
| D. Endurecimiento relacional | `EmpresaId` NOT NULL + índices `(EmpresaId,...)` + FKs compuestas que bloqueen relaciones entre empresas | Integridad SQL validada sobre datos de staging |
| E. Aislamiento en aplicación | `ITenantContext`, membresías por petición, filtros, escrituras validadas; adaptar todos los controladores y reportes; idempotencia y workers tenant-aware | Matriz A/B de tests y escenarios concurrentes aprobada |
| F. Lanzamiento de espacios | Activar pantalla de empresas/selector/invitaciones, creación de nueva empresa y aprovisionamiento transaccional; feature flag | Ninguna ruta administrativa/comercial accede a otro tenant; revisión de seguridad |
| G. Piloto cerrado | 2 empresas ficticias + usuarios compartidos/diferentes, backups/restore, logs, monitorización y CI | Sin fuga de datos y procedimiento operativo documentado |

Para el MVP no crear nueva empresa desde una pantalla pública hasta que la etapa E esté **completamente** cubierta; hacer aprovisionamiento controlado en staging antes.

## 8. Próximo módulo: **Empresas y espacios de trabajo**

La especificación de historias, interfaz, modelo, permisos y aceptación está en `docs/MODULO_EMPRESAS_Y_EQUIPOS.md`.

**Funcionalidades en orden:** registro controlado de empresa → identidad global → membresía → selector de empresa → invitados → perfiles/roles → suspensión/activación → auditoría. Suscripciones y pagos SaaS **después** de poder garantizar aislamiento.

## 9. Modernización tecnológica necesaria

Se revisó `SistemaFacturacion.csproj` de la rama: `net8.0` y referencias a EF Core 8.

Según Microsoft, **.NET 8 finaliza soporte el 10/11/2026**. Priorizar evaluación y migración a **.NET 10 LTS** en una rama separada, con SDK/Visual Studio compatibles, actualización de paquetes, migraciones EF y pruebas de regresión. No cambiar `TargetFramework` de forma aislada ni mezclar con todos los cambios de multiempresa en un único PR.

Fuentes oficiales:
- https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/
- https://learn.microsoft.com/en-us/ef/core/querying/filters
- https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy

## 10. Criterios mínimos para declarar Nexora SaaS funcional

- [ ] Empresa A/B con usuarios distintos y un usuario que pertenece a ambas, pero con roles diferentes.
- [ ] Un anónimo y un miembro inactivo NO acceden a ninguna ruta ni consulta empresarial.
- [ ] Todas las tablas comerciales etiquetadas por empresa, con claves/relaciones protegidas por SQL.
- [ ] Consultas, escritura, exportaciones, reportes, PDF, trabajos, caché y archivos sin fuga cruzada A/B.
- [ ] Usuario no puede elegir `EmpresaId` arbitrariamente desde HTML/URL para ganar permisos.
- [ ] Las ventas, garantías, inventario, posventa y anulaciones actuales funcionan dentro de cada tenant.
- [ ] Usuario/empresa suspendidos pierden acceso antes de emitir, modificar o cobrar.
- [ ] Dos empresas pueden utilizar el mismo SKU/número interno sin conflicto global si así se define.
- [ ] Migraciones y rollback probados con respaldo de staging; CI, monitoreo y auditoría implantados.
- [ ] Documentos siguen marcados como **internos no fiscales** hasta módulo fiscal DGII validado.
- [ ] Revisión de seguridad independiente antes de primeros clientes reales.

**Regla para el equipo:** trabajo técnico sin compilación/pruebas locales es `pendiente de validar`. PR #1 permanece en borrador; no publicar ni fusionar cambios tenant incompletos.

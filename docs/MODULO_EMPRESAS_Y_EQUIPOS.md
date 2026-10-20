# Nexora SaaS — Especificación del primer módulo: Empresas y equipos (SPEC-001)

**Estado:** especificación funcional + técnica, NO implementado.  
**Prerequisitos:** aislamiento multiempresa completo descrito en [ARQUITECTURA_SAAS.md](./ARQUITECTURA_SAAS.md), integración de autenticación/roles de Russell, migración por fases y respaldo.  
**Meta:** administrar empresas independientes, usuarios y permisos sin riesgo de consultar o modificar información de otra empresa.

## 1. Qué debe experimentar el cliente

**Escenario principal:** una tienda de electrodomésticos solicita Nexora, se crea su espacio de trabajo, su propietario invita a cajeros y accede a un tablero totalmente separado de otras tiendas.

### Flujo de alta propuesto

1. Una persona crea o verifica su cuenta global de Nexora.
2. Solicita crear `Empresa`, eligiendo nombre y slug disponible. Capturar datos legales/fiscales solo cuando corresponde; zona horaria `America/Santo_Domingo`, moneda `DOP` como valores iniciales, editables según jurisdicción futura.
3. Aprovisionamiento transaccional controlado crea empresa `PendienteConfiguracion`, membresía `Propietario`, valores básicos y evento de auditoría.
4. Usuario entra al asistente inicial: datos de negocio, usuarios, inventario inicial, sucursal/caja principal y configuración fiscal (modo comprobante interno hasta aprobación).
5. Empresa pasa a `Activa` cuando configuración, condiciones y validaciones técnicas estén completas. **No habilitar autoprovisionamiento público antes del aislamiento real**.
6. El propietario invita colaboradores con rol acotado; el invitado verifica identidad y acepta la membresía.
7. Usuario con varias membresías puede seleccionar una empresa: el sistema consulta permisos del usuario en **esa** empresa antes de servir datos.
8. Si caduca/suspende la suscripción, bloquear creación de operaciones según política contractual; mantener acceso controlado a exportación legal y registros según condiciones aplicables.

### Diseño textual de pantallas

**Selector de espacios de trabajo** (`/empresas`)
- Título `Mis empresas`; tarjetas con nombre, rol actual, estado, acceso.
- Botón `Crear empresa` **solo cuando sea seguro activar autoservicio**; inicialmente flujo controlado.
- Si una membresía está suspendida, mostrar motivo genérico y canal de soporte, **sin datos internos**.
- `Cambiar de empresa` valida membresía en servidor. No es suficiente modificar una cookie sin validación.

**Configuración empresarial** (`/e/{slug}/configuracion`)
- Nombre, marca/identidad, logo privado, teléfono comercial y dirección.
- Datos fiscales y dirección tributaria (separados de datos públicos), moneda y zona horaria.
- Estado de suscripción resumido; la facturación de la plataforma irá en sección separada.
- Alertas de permisos/seguridad, límites del plan y actividad administrativa.

**Equipo y permisos** (`/e/{slug}/equipo`)
- Usuarios, correo, rol en **esta** empresa, estado, último acceso (si auditable).
- `Invitar colaborador`: correo + rol permitido + vencimiento.
- Reenviar/revocar invitación, suspender membresía, cambiar rol; permisos específicos.
- Proteger al último propietario: no puede abandonar/deshabilitarse sin transferir propiedad.

**Asistente de incorporación**
- Checklist: completar empresa → invitar usuario → cargar catálogo → crear sucursal/caja → primera venta de prueba.
- Salir y continuar en otro momento; idempotencia al repetir pasos.

## 2. Modelo de datos esperado (MVP)

| Nombre | Tipo | Notas |
| --- | --- | --- |
| `Empresa.IdEmpresa` | `Guid` | Identificador interno estable, no editable |
| `Empresa.SlugNormalizado` | `nvarchar(63)` | Único a nivel global; no contiene secretos |
| `Empresa.NombreComercial` | `nvarchar(150)` | No necesariamente razón social |
| `Empresa.Estado` | Enum persistido `PendienteConfiguracion/Activa/Suspendida/Archivada` | Transiciones controladas |
| `Empresa.ZonaHoraria` | Cadena IANA | `America/Santo_Domingo` por defecto |
| `Empresa.Moneda` | ISO 4217 | `DOP` por defecto |
| `Empresa.FechaCreacionUtc` | `datetime2` | Auditable |
| `EmpresaUsuario.IdEmpresa` | `Guid` | FK Empresa |
| `EmpresaUsuario.UserId` | `nvarchar(450)` | FK `AspNetUsers.Id` |
| `EmpresaUsuario.Rol` | `Propietario`, `Administrador`, `Cajero` | Inicial; extensible vía permisos |
| `EmpresaUsuario.Estado` | `Activo/Invitado/Suspendido` | Roles nunca globales |
| `EmpresaUsuario.FechaAltaUtc` | `datetime2` | Inicio de membresía |
| `InvitacionEmpresa.TokenHash` | Hash fuerte | Solo token enviado por canal de invitación |
| `InvitacionEmpresa.EmailNormalizado` | `nvarchar(256)` | Verificar titularidad antes de aceptar |
| `InvitacionEmpresa.VenceUtc` | `datetime2` | Caducidad obligatoria |
| `InvitacionEmpresa.UsadoUtc` | nullable | Uso único |

**Restricciones:** PK `EmpresaUsuario(IdEmpresa, UserId)`; índice global único `Empresa.SlugNormalizado`; FK obligatorias; proveedor de invitaciones con límite de envíos y revocación; eventos de auditoría por cambio de usuario/rol/estado.

**Decisión sobre niveles:** `Suscripcion`, `Plan`, `FacturacionSaaS`, `CobroSuscripcion` se diseñarán en un módulo aparte, nunca como `Factura`/`Pago` comercial.

## 3. Servicios y límites técnicos previstos

- `ITenantResolver`: resuelve slug solicitado (ruta o host validado).
- `ITenantContext`: contexto scoped con empresa validada; falla si no hay tenant activo.
- `IMembershipService`: comprueba usuario, empresa, rol y estado contra fuente verificada.
- `IPermissionService`: decisión detallada por acción y empresa.
- `ICompanyProvisioningService`: creación transaccional e idempotente de empresa + propietario.
- `IInvitationService`: token de un solo uso, expiración, revocación, email verificado y auditoría.
- `ApplicationDbContext`: filtros tenant-aware en todas las tablas comerciales; validador de escrituras, índices y FKs compuestas.
- `PlatformDbContext` opcional **solo si la división de contextos aporta claridad**; si se usa un solo contexto, separar estrictamente APIs de plataforma y comercio.
- `IClock` o abstracción equivalente: registros en UTC en nueva lógica, zona horaria por empresa en pantalla; conservar compatibilidad con fechas existentes durante migración.

**Rutas sugeridas (borrador, sujetas a convención MVC):**

| Ruta | Acción | Alcance |
| --- | --- | --- |
| `GET /empresas` | selector | Solo autenticados |
| `GET /empresas/nueva` + `POST` | alta | Solo tras activar aprovisionamiento seguro |
| `GET /e/{slug}` | dashboard de empresa | Membresía activa |
| `GET /e/{slug}/configuracion` | configuración | Admin/Propietario |
| `GET /e/{slug}/equipo` | equipo | Admin/Propietario |
| `POST /e/{slug}/equipo/invitar` | invitación | Permiso `Equipo.Invitar` |
| `POST /e/{slug}/equipo/rol` | cambiar rol | `Equipo.Administrar`, no autoescalación |
| `POST /e/{slug}/equipo/suspender` | suspender miembro | Permiso, no suspender último propietario |
| `GET /invitaciones/aceptar/{token}` | mostrar invitación | Token validado, respuesta sin revelar información sensible |
| `POST /invitaciones/aceptar` | aceptar | Autenticación + email verificado + token válido, antiforgery |

Nunca exponer servicios de gestión comercial por `/e/{slug}` sin que el middleware de aislamiento esté activo. Evitar que slugs de otra empresa aparezcan en los errores.

## 4. Flujos de estados

### Empresa
- `PendienteConfiguracion → Activa` tras validaciones.
- `Activa → Suspendida` por decisión administrativa o regla contractual que debe auditarse.
- `Suspendida → Activa` tras validación/reinstalación de membresías y plan.
- `Archivada`: no se borran documentos comerciales para eludir obligaciones de conservación; definir política de exportación/retención.

### Miembros
- `Invitado → Activo` tras aceptación verificada.
- `Activo → Suspendido`: efecto inmediato; denegar nuevas operaciones aunque quede sesión web abierta.
- `Suspendido → Activo` solo con permiso apropiado.
- No permitir degradar/eliminar al **último Propietario** ni autoconcederse permisos mediante POST.

### Invitaciones
- Creada → aceptada, expirada o revocada.
- Token opaco, aleatorio, hash almacenado, verificación antes de vincular, uso único, tiempo limitado.
- Una invitación jamás concede acceso a otra empresa.

## 5. Casos de aceptación (bloqueantes del SaaS)

1. A administra productos y clientes, B tiene productos propios. A intenta entrar con la URL de B: denegado, sin fuga de nombres/IDs.
2. Mismo usuario pertenece a A como Admin y B como Cajero: en B **no** puede cambiar roles/precios si ese permiso está denegado, aunque sea Admin de A.
3. Usuario expulsado de A continúa en B si mantiene membresía activa; pierde A de inmediato.
4. Usuario anónimo no puede abrir `/e/a/facturas/1`, `/e/a/productos/1`, PDF, reportes, exportación o POS.
5. Todo POST intenta introducir `IdEmpresa = B`: se ignora y/o rechaza; nunca se crea objeto bajo otro tenant.
6. Factura A con `IdCliente` / `IdProducto` de B → error servidor y de FK, sin ningún cambio ni fuga de información.
7. Ajuste de stock, `ExecuteUpdateAsync`, consulta `AsNoTracking`, facturas y posventa A no operan sobre filas de B.
8. Empresas A/B utilizan el mismo SKU cuando se permite sin colisión de unicidad global.
9. Un usuario sin rol para `Equipo.Invitar` no puede enviar invitación por POST ni activar cambios a través de la API.
10. Aceptar token vencido, usado o de email distinto nunca crea membresía.
11. Dos registros simultáneos de la misma empresa/invitación no duplican membresías o empresas (idempotencia).
12. No se puede suspender al último propietario de la empresa.
13. No se acepta un `Slug` arbitrario como prueba de pertenencia.
14. Trabajos en segundo plano, informes, auditorías, correo y caché no mezclan información A/B.
15. Backup/restore de staging, migraciones y pruebas E2E/seguridad aprobadas antes de habilitar autoservicio real.

## 6. División propuesta de trabajo

**Russell — autenticación y autorización**
- Identity, sesiones, roles per-empresa/miembros, invitaciones y requisitos de permisos.
- Entregar rama, contratos `IMembershipService`/`IPermissionService`, pruebas para Administrador/Cajero y configuración de registros.
- Coordinar antes de editar `Program.cs`, `ApplicationDbContext` y modelos de usuarios; son puntos de integración compartidos.

**Equipo Nexora comercial**
- `EmpresaId` y claves compuestas en datos comerciales; adaptar Clientes, Categorías, Productos, Facturas, Detalles, Pagos, Posventa y Reportes.
- Scoping en operaciones atómicas de stock y anulación, archivos, impresiones y eventos.
- Pruebas cross-tenant A/B y migración de datos existentes.

**Integración y revisión conjunta**
- Definir quién es dueño de middleware/tenant context/servicios para evitar conflictos.
- Revisar cada PR por separado; un PR de migración por fase; `master` estable.
- Nadie fusiona una ruta parcialmente multiempresa donde los demás controladores siguen sin restricciones.

## 7. Primera entrega técnica (Sprint SaaS 1) — segura y limitada

**Entrega 1A: contratos y tablas de plataforma, sin activar nuevos tenants en producción**
- Interfaces y entidades `Empresa`, `EmpresaUsuario`, estados y configuraciones.
- Migración **aditiva** de tablas de plataforma (sin `EmpresaId` agregado aún a ventas).
- Tests de migración y constraints, restauración de respaldo; no exponer registro público ni gestión de empresa hasta aislamiento.

**Entrega 1B: aislamiento comercial completo**
- Migraciones de backfill a `EmpresaDemo`.
- Todas las entidades y FKs compuestas, filtros EF y validación de POST/IDs.
- Pruebas negativas y de concurrencia para A/B; sin endpoints abiertos.

**Entrega 1C: UX Empresas/Equipo**
- Selector, incorporación, invitaciones, membresías, permisos y auditoría.
- Después, habilitar aprovisionamiento, con feature flag y piloto ficticio.

**Regla de salida:** completar 1A y 1B y validar permisos Russell ANTES de activar 1C para clientes. `SaaS listo` no significa solamente ver varias empresas en pantalla.

# Nexora SaaS — Fase 1B-2B: verificación de membresías (componente preparado)

**Fecha:** 21/10/2026.  
**Rama:** `feature/nexora-ui`.  
**Estado:** código preparado; **NO integrado en el pipeline web** y **NO validado con compilación/SQL reales** desde este entorno.

## Propósito

Se agregó un servicio reutilizable **solo de consulta** que valida acceso a una empresa usando exclusivamente la identidad autenticada y la base de datos, sin confiar en una empresa suministrada por el navegador.

### Archivos nuevos

- `Services/Tenancy/IValidadorAccesoEmpresa.cs`: `ValidarAsync(ClaimsPrincipal? usuario, string? slugCandidato, CancellationToken)`.
- `Services/Tenancy/ValidadorAccesoEmpresaSql.cs`: implementación parametrizada por `Microsoft.Data.SqlClient`; consulta directamente `Empresas` y `EmpresaUsuarios` **sin utilizar `ApplicationDbContext`**, de modo que no existe ciclo de dependencias con `IContextoEmpresa`.
- `Services/Tenancy/MembresiaEmpresaValidada.cs`: DTO inmutable que contiene `IdEmpresa` validado y el rol de esa membresía.

### Reglas de acceso — se deniega si ocurre cualquiera

1. Usuario no autenticado, sin claim estándar `ClaimTypes.NameIdentifier`, identificador vacío o longitud mayor que 450.
2. Slug no válido: longitud fuera de 3–63, caracteres distintos a letras ASCII minúsculas/números/guiones o guiones inicial/final.
3. La empresa solicitada no está registrada, no está `Activa`, o su slug no coincide.
4. No existe vínculo para ese `UserId` y esa empresa con `Estado = Activo` y `FechaBajaUtc IS NULL`.
5. El rol no es `Propietario`, `Administrador` o `Cajero`.
6. Base de datos inaccesible o con error: la operación falla con excepción, **sin conceder permiso por defecto**.

El valor de `slugCandidato` se normaliza y verifica; **solamente propone la empresa**, nunca prueba membresía. La consulta se evalúa en SQL en **cada llamada** para respetar inmediatamente la suspensión o retiro de membresías. Todos los parámetros usan `SqlParameter`, sin concatenar entradas en SQL.

### Integración FUTURA, no activar aún

En la fase de integración coordinada con Russell:

1. Definir qué componente de su módulo proporciona `ClaimsPrincipal` de Identity y cómo se selecciona empresa (slug de ruta/selector).
2. Registrar `IValidadorAccesoEmpresa` en DI **junto a** Identity, sin crear roles globales de acceso comercial.
3. Después de `UseAuthentication()`, validar membresía *antes* de acciones comerciales; colocar solo el `IdEmpresa` aprobado en un `IContextoEmpresa` **scoped**. No permitir que controllers/POST/session fijen libremente `IdEmpresa`.
4. En los endpoints comerciales exigir autenticación + permisos por acción y empresa. Revalidar si el rol/membresía cambia; tener en cuenta el último Propietario.
5. No usar `ApplicationDbContext` dentro del validador (causaría un ciclo de DI).
6. Implantar controles SQL de FKs compuestas `(IdEmpresa, Id...)`; eliminar defaults legados; auditar `ExecuteUpdate`, SQL crudo, exportaciones e impresión.
7. Confirmar mediante pruebas negativas con usuarios A/B y empresas A/B. Solo entonces permitir el segundo tenant.

**IMPORTANTE:** el único servicio `IContextoEmpresa` registrado actualmente sigue siendo `ContextoEmpresaDesarrollo`. No se modificó `Program.cs` ni la autenticación existente. Por tanto, este nuevo verificador **no cambia todavía el comportamiento observable del sistema** y **no representa autorización SaaS real**.

La empresa `nexora-legado` actual está en estado `PendienteConfiguracion` y **no debe pasar el verificador**. No cambiarla a `Activa` ni crear membresías manualmente para "forzar" estas pruebas antes de la integración de roles.

## Plan de validación local

1. `git switch feature/nexora-ui` y `git pull origin feature/nexora-ui`.
2. Compilar en Visual Studio `Ctrl + Shift + B`; verificar que no aparezcan errores nuevos. No ejecutar `Add-Migration` ni `Update-Database`: no se cambió el modelo de datos.
3. Confirmar que Clientes, Productos, Facturas, Garantías, Posventa y Reportes siguen funcionando **en modo Development local**, tal como antes (el validador aún no está conectado).
4. Verificar adicionalmente el estado Agotado/Reposición/Desactivación siguiendo [`ESTADO_INVENTARIO_AUTOMATICO_PRUEBAS.md`](ESTADO_INVENTARIO_AUTOMATICO_PRUEBAS.md).
5. Cuando Russell termine su módulo, en una **base de pruebas aislada con usuarios y empresas de prueba**, validar los siguientes casos con pruebas de integración que invoquen al servicio:

| Usuario | Empresa | Membresía | Estado empresa | Resultado esperado |
| --- | --- | --- | --- | --- |
| Anónimo | A | cualquiera | Activa | Acceso denegado |
| Usuario A | A | Activo / Cajero | Activa | Membresía válida (el permiso de acción se determina aparte) |
| Usuario A | B | no existe | Activa | Acceso denegado |
| Usuario A | A | Suspendido | Activa | Acceso denegado |
| Usuario A | A | Invitado | Activa | Acceso denegado |
| Usuario A | A | Activo pero `FechaBajaUtc` no es NULL | Activa | Acceso denegado |
| Usuario A | A | Activo | Suspendida / Archivada / PendienteConfiguracion | Acceso denegado |
| Usuario A | slug malformado | Activo | Activa | Acceso denegado |
| Usuario A | A | Rol fuera de política | Activa | Acceso denegado |

**No crear una segunda empresa en la base habitual.** Ejecutar estos casos únicamente en un ambiente de pruebas segregado y con respaldo.

## Bloqueantes abiertos

- [ ] Compilación local de este componente.
- [ ] Módulo Russell de autenticación/roles listo y revisado.
- [ ] Integración de contexto scoped validado + protección de todos los controladores.
- [ ] Eliminación de default heredado en EF y SQL Server.
- [ ] Restricciones compuestas en relaciones comerciales.
- [ ] Pruebas A/B completas (incluyen `FindAsync`, filtros, SQL de actualización, anulación, impresión y reportes).
- [ ] Auditoría de autorización por rol y registro de operaciones sensibles.
- [ ] Autorización explícita de release a producción.

Hasta cerrar todos estos puntos, no fusionar PR #1 a `master`, ni abrir el SaaS al público.

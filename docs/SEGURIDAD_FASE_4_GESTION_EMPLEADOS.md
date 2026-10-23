# Nexora SaaS — Fase 4: invitaciones, roles y membresías auditadas

**Rama:** `feature/nexora-security-phase4-members` → `feature/nexora-security-phase3-team`  
**Estado:** código enviado para revisión. Aún **sin compilación .NET 8 ni pruebas SQL Server reales desde el asistente**. No apto para producción.

## Qué se implementó

- **Mi equipo** incluye formulario para **generar** una invitación para un correo y rol `Cajero` o `Administrador`, y controles para cambiar rol o suspender/reactivar empleados no propietarios.
- El enlace de invitación es de **un solo uso, 48 horas, aleatorio criptográficamente (32 bytes), almacenado en SQL solo como SHA-256**. Se muestra **una vez** al propietario, para entrega manual por canal privado. **NO se envía por email**. Para compartir una URL absoluta debe configurarse `Seguridad:BaseUrlPublica` con HTTPS; nunca se usa directamente el encabezado HTTP Host del navegador para construirla.
- La aceptación usa las únicas rutas `GET/POST /invitaciones/aceptar`, requiere **Identity autenticado, EmailConfirmed, NormalizedEmail igual al destinatario**, empresa `Activa`, creador aún `Propietario` vigente, token `Pendiente` y no vencido, y que la cuenta no pertenezca ya a la empresa. El POST requiere antiforgery. La excepción al filtro empresarial está limitada a estas acciones porque el destinatario todavía no es miembro; **el controlador exige [Authorize]**.
- Después de aceptar, se crea una membresía `Activo` en `EmpresaUsuarios`, se marca token `Aceptada` y se registra auditoría en **una misma transacción SQL**.
- Para cambiar roles o estado: verifica actor Propietario vigente y empresa Activa **dentro de transacción SERIALIZABLE**. Requiere rowversion de la membresía, mismo tenant, target distinto al actor, cuenta no dada de baja; solo transiciones válidas. No permite modificar ni ascender a `Propietario` en esta pantalla. Los dos tipos de operación requieren CSRF.
- Suspender se refiere **solo a la membresía empresarial**, no al login global de Identity. El middleware de tenancy consulta membresía activa en SQL en nuevas peticiones; no se tocan otras empresas del mismo usuario.
- Nuevas tablas:
  - `InvitacionesEmpresa`: IdEmpresa, EmailNormalizado, Rol, TokenHash, Estado, vencimiento, emisor y datos de consumo. Índice único por token y por email pendiente dentro de una empresa.
  - `AuditoriaMembresias`: actor, persona afectada/invitación, tipo, rol y estado antes/después y fecha UTC. Solo INSERT desde las operaciones del servicio. El rollback automático de la migración se bloquea para no borrar auditorías accidentalmente.
- La página muestra los **20 eventos de auditoría recientes** de la empresa autorizada. No consulta ni revela otras empresas.

## Qué NO se ha implementado

- **Envío automático de correos ni infraestructura de confirmación de correo**. Para aceptar una invitación, el destinatario debe tener **una cuenta Identity con ese correo confirmado**. Un registro nuevo sin método de confirmación funcional podría quedar bloqueado. Esta limitación impide considerar las invitaciones aptas para onboarding público todavía.
- Revocación manual de invitaciones pendientes y edición de cuentas `Propietario`. Los enlaces vencen a las 48 h; revocación es mejora necesaria antes de uso real.
- Selector de empresa y propagación de slug en todas las vistas comerciales.
- Auditoría SQL protegida contra usuarios con permisos directos de UPDATE/DELETE en la base; se requiere endurecimiento de permisos de base de datos en producción.
- Pruebas de concurrencia reales, análisis de cookies/sesiones existentes y configuraciones de envío/dominio.
- Confirmaciones de dinero, recepciones físicas ni ajustes de inventario.

## Archivos

- `Services/Tenancy/GestionMembresiasSegura.cs`: flujo transaccional de roles, estados, invitaciones y auditoría con SQL parametrizado.
- `Controllers/EquipoController.cs`: acciones POST con antiforgery y políticas, directorio y actividad de seguridad.
- `Controllers/InvitacionesController.cs`: aceptación Identity segura, sin pertenencia previa.
- `Security/ReglasAccesoControladores.cs`: permisos explícitos para nuevos POST.
- `Security/FiltroPermisosComerciales.cs`: única excepción al filtro tenant para aceptar invitaciones; exige sesión autenticada.
- `Program.cs`: registra servicio scoped.
- `ViewModels/EquipoEmpresaViewModel.cs` y Razor en `Views/Equipo`, `Views/Invitaciones`; CSS responsive.
- `Data/Migrations/20261023184000_EquipoSeguridadAuditoriaInvitaciones.cs`: migración SQL para las tablas independientes sin nuevas entidades EF Core ni cambios en snapshot.
- `tests/Nexora.Security.Checks/Program.cs`: **38 verificaciones preparadas** (las anteriores más reglas de esta fase).

## Pruebas paso a paso

1. Usar solo una **base de pruebas aislada**, con backup y capacidad de restauración comprobada. **No aplicar migración en producción ni en una base con datos de clientes reales**.
2. En Visual Studio, actualizar rama:
   ```powershell
   git fetch origin
   git switch feature/nexora-security-phase4-members
   git pull origin feature/nexora-security-phase4-members
   dotnet build SistemaFacturacion.csproj
   dotnet run --project tests/Nexora.Security.Checks/Nexora.Security.Checks.csproj
   ```
   Esperado: compilación correcta y **38 pruebas con cero errores**. Si falla, NO tocar la base de datos.
3. Con el proyecto compilado y backup de laboratorio, revisar migraciones con `Get-Migration`. Verificar que esté la migración `EquipoSeguridadAuditoriaInvitaciones`. **Solo en la BD de prueba**, aplicar `Update-Database` y revisar ambas tablas.
4. Establecer `Seguridad:ActivarTenancyValidada=true` solo en **laboratorio**, con Identity, empresa `Activa` y propietario `Activo`. Preparar `Seguridad:BaseUrlPublica=https://dominio-de-prueba` o usar la ruta relativa en el mismo host local, sin compartir un link falso.
5. Abrir `/e/{slug}/Equipo/Index` como propietario. Invitar a usuario Identity con correo confirmado, `Cajero`. Copiar la URL una vez y entregarla por privado; comprobar que en SQL aparece token hash (32 bytes), nunca el token en claro. No debería poderse duplicar la misma invitación pendiente.
6. En otra sesión, como el usuario invitado y con correo confirmado coincidente, visitar el enlace y aceptar. Debe crear membresía `Activo`, pasar token a `Aceptada` y generar auditoría. Un segundo intento debe fallar **sin crear membresía adicional**.
7. Probar con correo distinto, no confirmado, anónimo, empresa suspendida, rol inválido, creador revocado y token vencido: deben rechazarse sin escritura.
8. Como propietario, cambiar `Cajero ↔ Administrador` en un empleado que no es propietario; confirmar entrada de auditoría. Volver a usar una versión de formulario anterior: debe rechazarse por RowVersion.
9. Suspender la membresía de ese empleado: su próxima solicitud de empresa debe denegarse. Reactivar y confirmar reversión del bloqueo en ese tenant. Su sesión Identity global no debe quedar revocada en otras empresas.
10. Intentar modificar al propio actor o a otro `Propietario`, crear un `Propietario` desde invitaciones o actualizar una empresa distinta manipulando campos POST: operación denegada.
11. Probar dos cambios simultáneos, token aceptado dos veces en paralelo y cambios de propietario en otra sesión. Auditar los resultados de SQL, no dar estas pruebas por pasadas hasta ejecutarlas.
12. Revisar `AspNetUsers`, `EmpresaUsuarios`, `AuditoriaMembresias`, `InvitacionesEmpresa` para confirmar que no hubo efectos colaterales.

## Configuración de seguridad

Por defecto, `Seguridad:ActivarTenancyValidada` sigue apagado. **No habilitarlo en la base normal** ni crear un segundo tenant. Los endpoints nuevos del Equipo están protegidos por atributos incluso con la flag desactivada. La aceptación de invitación devuelve 404 con la flag apagada.

Para configurar URL pública en pruebas, usar User Secrets o variables de entorno (no escribir secretos/tokens en Git):

```powershell
dotnet user-secrets set "Seguridad:BaseUrlPublica" "https://tu-dominio-de-pruebas.example"
```

**Atención:** esto solo configura la base de un enlace, NO un servidor web, dominio ni un servicio de envío de correos.

## Publicación bloqueada

No fusionar a `master`, no abrir Nexora al público, ni declarar seguro el onboarding hasta tener email sender/confirmación, revocación, pruebas A/B en SQL, validación de rutas comerciales e inspección de permisos por los controladores. Mantener PR de esta fase en borrador.

# Nexora SaaS — Fase 5: bandeja y revocación auditada de invitaciones

**Rama:** `feature/nexora-security-phase5-invites` → `feature/nexora-security-phase4-members`.  
**Estado:** fuente y revisión estática completadas; **compilación, 45 pruebas y casos SQL reales pendientes**. No apto para producción.

## ¿Por qué esta fase?

La fase 4 genera enlaces de 48 horas y permite aceptarlos una sola vez. Faltaba una forma de **invalidar inmediatamente** un enlace enviado a un destinatario incorrecto. Un simple botón que esconda el enlace no lo invalidaría; la revocación ahora modifica la fila en SQL Server, impidiendo la aceptación posterior.

## Cambios implementados

- **Bandeja de invitaciones** en `GET /e/{empresaSlug}/Equipo/Invitaciones`, accesible desde «Mi equipo», **solo para Propietario con membresía activa y empresa Activa**.
- Filtros con lista cerrada: `Pendiente` (vigente), `Vencida` (estado en BD Pendiente, plazo superado), `Aceptada`, `Revocada`, `Caducada` y `Todas`. La caducidad visual no finge haber escrito un evento en BD.
- Paginación SQL con 20 filas por página y `IdEmpresa` validado en **todas** las consultas. Listado incluye solo destinatario, rol, estado, creador y fechas; nunca devuelve hash ni token original.
- **POST `/e/{empresaSlug}/Equipo/RevocarInvitacion`**, con anti-CSRF y policy `AdministrarUsuarios`. Para revocar:
  1. Validar la identidad del propietario y su membresía activa **directamente en SQL**, dentro de `SERIALIZABLE`.
  2. Comprobar que la invitación pertenece a **la empresa verificada**; estado `Pendiente` y `VenceUtc > SYSUTCDATETIME()`.
  3. Actualizar a `Revocada` solo si esas condiciones siguen siendo ciertas, con comprobación de una fila modificada.
  4. Registrar `InvitacionRevocada` en `AuditoriaMembresias` **en la misma transacción**.
  5. Confirmar la transacción únicamente después de guardar la auditoría.

- La aceptación de la fase 4 exige explícitamente `Estado == "Pendiente"`, por lo que un enlace revocado se vuelve inservible.
- No afecta a usuarios que ya aceptaron el enlace. Para bloquear a un empleado activo debe **suspenderse su membresía en Mi equipo**.
- Se añaden 7 pruebas estáticas/lógicas al runner, para un objetivo total de **45 pruebas**.
- Nueva migración `20261024143000_RegistrarRevocacionInvitaciones`: amplía la restricción SQL `CK_AuditoriaMembresias_Accion` para permitir `InvitacionRevocada`. `InvitacionesEmpresa.Estado` ya permitía `Revocada`, así que no cambia de estructura ni necesita reconstruirse el snapshot EF.

## Archivos principales

- `Services/Tenancy/GestionMembresiasSegura.cs`: listado paginado y revocación auditada.
- `ViewModels/InvitacionesEmpresaViewModel.cs`: proyección sin tokens.
- `Controllers/EquipoController.cs`: GET y POST protegidos.
- `Views/Equipo/Invitaciones.cshtml`: filtros, tabla y revocación.
- `Views/Equipo/Index.cshtml`: acceso desde Mi equipo.
- `Security/ReglasAccesoControladores.cs`: permiso GET/POST explícito, deny-by-default.
- `Data/Migrations/20261024143000_RegistrarRevocacionInvitaciones.cs`: nuevo valor del check constraint de auditoría.
- `tests/Nexora.Security.Checks/Program.cs`: 45 casos en total.
- `wwwroot/css/site.css`: estilos responsive.

## Prueba de compilación (sin tocar SQL)

En Visual Studio, desde la carpeta de `SistemaFacturacion.csproj`:

```powershell
git fetch origin
git switch feature/nexora-security-phase5-invites
git pull origin feature/nexora-security-phase5-invites
dotnet build SistemaFacturacion.csproj
dotnet run --project tests/Nexora.Security.Checks/Nexora.Security.Checks.csproj
```

Objetivo: **45 pruebas, 0 errores**, pero aún no se ejecutaron aquí. Si falla la compilación o un test, no continuar con la migración.

## Prueba SQL de laboratorio (OBLIGATORIA antes de integrar)

**No aplicar migraciones a la base de Nexora habitual/producción.** Usar un laboratorio aislado, con copia/backup validado y usuarios ficticios. La migración de fase 4 (`EquipoSeguridadAuditoriaInvitaciones`) debe haberse aplicado antes de esta fase.

1. En Visual Studio Package Manager Console, apuntando explícitamente a la BD de laboratorio, consultar `Get-Migration`. Revisar que la base y connection string son **las de laboratorio**.
2. Ejecutar `Update-Database` solamente en esa BD de prueba. Comprobar que `CK_AuditoriaMembresias_Accion` incluye `InvitacionRevocada`.
3. Preparar Propietario A activo en Empresa A activa, con flag `Seguridad:ActivarTenancyValidada=true` únicamente en laboratorio. No manipular `nexora-legado` en la base habitual.
4. Crear invitación ficticia para `empleado-a@example.com` desde A; comprobar que aparece en **Invitaciones / Pendientes** sin token hash visible. Confirmar que se puede revocar con un único POST autorizado.
5. Intentar aceptar el enlace previamente generado como destinatario confirmado **después** de revocarlo: debe mostrar rechazo; no debe crearse membresía.
6. En SQL: `InvitacionesEmpresa.Estado = 'Revocada'`; `AuditoriaMembresias` tiene exactamente un registro `InvitacionRevocada`, con mismo `IdEmpresa`, actor y `IdInvitacion`.
7. Repetir el POST sobre el mismo GUID: debe fallar y no crear una segunda auditoría.
8. Crear invitación y **aceptarla antes de revocar**: el botón ya no debe ofrecerse, la petición directa debe fallar y no revertir membresías.
9. Intentar revocar un enlace vencido: debe rechazarse; el filtro **Vencida** muestra la invitación sin alterar su estado en BD.
10. Administrador/Cajero A: GET a la bandeja y POST de revocación → denegados. Anónimo → Challenge. Si se desactiva/suspende al Propietario actor, debe rechazarse incluso con un formulario antiguo.
11. Propietario A con GUID de una invitación de Empresa B: no debe acceder, revocar ni ver datos de B. Probar con dos empresas ficticias en base **separada**.
12. Dos revocaciones simultáneas o aceptación concurrente: una sola operación válida y auditoría consistente; ninguna membresía creada con token ya revocado.
13. Filtros y navegación de páginas: solo registros de la empresa activa. No exponer tokens, contraseñas, hashes ni datos de otros tenants.
14. Confirmar regresión: facturas, productos, stock, pagos, reembolsos y recepciones siguen sin modificarse.

## Pendientes y límites

- Envío automático de emails, entrega segura del correo de confirmación Identity y reenvío seguro de invitaciones todavía no implementados.
- Anulación de una invitación ya aceptada NO revoca una membresía, para eso existe suspensión.
- Pruebas SQL de concurrencia, antiforgery y autorización HTTP reales están pendientes, fuera del runner unitario.
- Completar selector de empresa y navegación multiempresa antes de activar un SaaS abierto.
- Mantener el PR en **DRAFT** y no fusionar a `master`, no activar `Seguridad:ActivarTenancyValidada` en la base normal.

# Nexora SaaS — Seguridad fase 3: Mi equipo y matriz de permisos (solo lectura)

**Rama:** `feature/nexora-security-phase3-team` basada en `feature/nexora-security-phase2` (que depende de fase 1).  
**Estado:** implementación lista para revisar; compilación y pruebas en SQL/HTTP **pendientes de ejecutar localmente**. No se habilita multiempresa en producción.

## Alcance entregado

- Nuevo **GET `/e/{empresaSlug}/Equipo/Index`** mediante el controlador `EquipoController` y la vista `Views/Equipo/Index.cshtml`.
- Lista de empleados/membresías de la empresa verificada: nombre Identity, correo Identity, correo confirmado, rol empresarial, estado y fecha de alta UTC. No muestra IDs, hashes, tokens, security stamps, números de recuperación, datos de pago ni secretos.
- Solo pueden verla usuarios **con identidad autenticada, empresa Activa, membresía Activa y rol Propietario**. Se exigen conjuntamente:
  1. El middleware y contexto `ContextoEmpresaVerificado` de las fases anteriores (cuando el modo validado está activado).
  2. El filtro comercial `ReglasAccesoControladores.ObtenerPermiso("Equipo", "Index", "GET") => AdministrarUsuarios`.
  3. El atributo `[Authorize(Policy = PermisosNexora.AdministrarUsuarios)]` del controlador.
  4. Una comprobación adicional dentro de `EquipoController` de membresía/permiso y de estado Activa de la empresa.
  5. La consulta sobre `EmpresaUsuarios` filtra explícitamente **`m.IdEmpresa == _empresa.IdEmpresa`**, ya que esta tabla global **NO tiene filtro EF automático por tenant**.
- Indicadores para toda la empresa autorizada: activos, propietarios activos, invitados y suspendidos. Miembros encontrados y filas de tabla reflejan los filtros y paginación.
- Filtros por rol/estado sujetos a una lista cerrada, incluyendo membresías `Baja` (`FechaBajaUtc != null`), y paginación de 20 resultados por página.
- Matriz de permisos derivada de `PermisosNexora.Todos` y `PermisosNexora.TienePermiso(rol, permiso)`, evitando mantener cifras divergentes en la UI. Los permisos financieros de confirmación **están reservados**, pero no hay endpoint para usarlos.
- Nuevo menú **Mi equipo** solo si el contexto de esa petición contiene una empresa verificada con el permiso `AdministrarUsuarios`. Ocultar el enlace no sustituye el enforcement servidor.
- No se han añadido botones/POST para invitar usuarios, crear cuentas, activar/suspender membresías, cambiar roles o eliminar propietarios.

## Seguridad y privacidad

- Identity almacena usuarios globales, pero `EmpresaUsuario` define su rol **dentro de cada empresa**. No consultar `Identity.Users` global sin partir de la membresía filtrada.
- El correo de un empleado solo se presenta al propietario autorizado de su empresa; la página no debe indexarse ni ofrecer exportaciones públicas.
- Una empresa suspendida, usuario anónimo, membresía caducada/suspendida o usuario de otra empresa no puede visualizar el directorio.
- Se mantienen apagadas por defecto `Seguridad:ActivarTenancyValidada` y las nuevas políticas en el Development mononegocio convencional.
- El controlador nuevo permanece protegido por atributo **también cuando la bandera está apagada**, por lo que NO se abrirá en tu Development habitual. Esto es intencional: sin membresía verificada no se consultan identidades.
- Para probar la UI se necesita **base de laboratorio separada y aislada** con empresas/usuarios de prueba y la bandera activada; no modifiques `nexora-legado` ni crees administradores privilegiados manualmente en datos habituales.
- Todo es solo lectura: no se editan `EmpresaUsuarios`, `AspNetUsers`, productos, recibos, facturas, reembolsos ni inventario.

## Pruebas locales

En terminal de Visual Studio:

```powershell
git fetch origin
git switch feature/nexora-security-phase3-team
git pull origin feature/nexora-security-phase3-team
dotnet build SistemaFacturacion.csproj
dotnet run --project tests/Nexora.Security.Checks/Nexora.Security.Checks.csproj
```

**Resultado esperado del runner:** 28 verificaciones aprobadas, cero fallos. Si aparece un error, detener y compartir salida completa. Esta cifra es un objetivo de pruebas, **no un resultado ejecutado por el asistente**.

En modo habitual (`Seguridad:ActivarTenancyValidada=false`):
1. Abrir Nexora Development y probar los módulos comerciales, reembolsos pendientes, recepción simulada y Control de Anulaciones. Deben seguir funcionando sin nuevas pantallas obligatorias.
2. No debe mostrarse el menú **Mi equipo**.
3. `/Equipo/Index` debe seguir denegado aunque el usuario haya iniciado sesión: no hay contexto empresarial verificado.
4. No ejecutar `Add-Migration` ni `Update-Database`. No se modificó EF Core.

En **un laboratorio aislado con empresa A/B y usuarios A/B**, activar la bandera sin compartir secretos:
1. Propietario activo de A entra en `/e/empresa-a/Equipo/Index`: verá exclusivamente los miembros de A.
2. Propietario A intenta `/e/empresa-b/Equipo/Index` sin membresía B: acceso denegado, jamás se muestran miembros de A ni B.
3. Administrador activo de A intenta la ruta: denegado.
4. Cajero activo de A intenta la ruta: denegado.
5. Usuario anónimo: redirección de Challenge a login.
6. Empresa suspendida o membresía suspendida: acceso denegado.
7. Filtros `rol`, `estado` y `pagina` solo muestran las membresías filtradas dentro de A. Los estados `Baja` requieren fecha de baja no nula.
8. Nombre, correo y correo confirmado deben coincidir con Identity de prueba; verificar ausencia de hashes/tokens en la respuesta HTML/JSON.
9. Probar más de 20 personas con paginación, y verificar que el enlace no se muestra a usuarios sin permiso.
10. Intentar POST a `/e/empresa-a/Equipo/Index`, `Equipo/Create`, `Equipo/CambiarRol`: ninguno puede modificar usuarios ni roles. Si no existe ruta, obtener 404/405; en todo caso cero efectos.
11. Comparar con SQL: ningún cambio en `EmpresaUsuarios`, `AspNetUsers`, stock, facturas, reembolsos o recepciones tras navegar.

## Antes de permitir cambios reales de roles

Queda bloqueado y debe diseñarse/revisarse por separado:

- Alta por **invitación segura** con token aleatorio de uso único, almacenado hasheado, expiración, revocación, correo verificado, antiabuso y auditoría.
- Autorización individual de cambios: quien actúa no puede asignarse permisos superiores ni transferir propiedad sin aprobación especial; protección del **último propietario activo** en una transacción SQL.
- `[ValidateAntiForgeryToken]`, RowVersion/concurrencia, operación idempotente y revalidación inmediata de membresía + permiso antes de escribir.
- Auditoría inmutable: actor, empresa, sujeto, antes/después, UTC, motivo, correlation id, sin registrar contraseñas ni tokens.
- Restricciones en base de datos para altas/suspensiones y pruebas adversariales (dos admins concurrentes, dos empresas, sesión revocada).
- UX de selección de empresa, rutas con slug en toda la navegación y política de registros de Identity; **el resto de los enlaces aún requiere revisión de tenancy con la bandera activa**.
- No abrir el SaaS al público ni fusionar a `master` hasta completar estas comprobaciones.

**No confundir directorio de lectura con un módulo ya habilitado para administrar empleados.**

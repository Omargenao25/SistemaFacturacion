# Nexora — Fase 6: selector de empresa y navegación con contexto verificado

**Branch:** `feature/nexora-security-phase6-company-selector`  
**Base:** `feature/nexora-security-phase5-invites` (PR #6).  
**Estado:** código subido con comprobación estática pendiente de build/ejecución locales. No apto para producción.

## Problema que resuelve

Antes, el módulo de permisos de Nexora esperaba una ruta `/e/{empresaSlug}/...` pero no había una pantalla para encontrar las empresas permitidas ni una entrada segura desde `/`. Por eso no era posible navegar fácilmente a los módulos de equipo e invitaciones en el laboratorio.

## Implementación

- `GET /mis-empresas` requiere Identity **autenticado** y solo existe funcionalmente con `Seguridad:ActivarTenancyValidada=true`. Con flag OFF devuelve 404 y no altera el modo Development legado.
- El nuevo `EmpresasAccesoController` lee `EmpresaUsuarios` **filtrado por UserId de la cookie Identity validada, membresía Activo sin baja, rol reconocido y empresa Activa**. Es una tabla global SIN query-filter por tenant: la consulta nunca puede convertirse en un listado de todas las empresas. Pagina 12 resultados por página.
- Tarjetas muestran nombre, slug, rol y moneda; no exponen otros usuarios, correos, tokens ni IDs globales. Los slugs pasan por una validación estricta de ASCII, números y guiones.
- Seleccionar una empresa genera un enlace `/e/{empresaSlug}/Home/Index`. **No guarda el tenant en la cookie, session, URL de redirección externa ni un claim**. Cada nueva petición con slug vuelve a validarse por `ValidadorAccesoEmpresaSql` y los permisos de `PermisoEmpresaHandler`.
- `SlugEmpresaSeguro` unifica la validación del selector y el validador SQL. El slug elegido sigue siendo solo un candidato, nunca un permiso.
- El filtro comercial `FiltroPermisosComerciales` permite exclusivamente `EmpresasAcceso.Index.GET` con usuario Identity autenticado; deniega demás acciones/métodos desconocidos. El controlador lleva `[Authorize]` redundante y no tiene POST.
- Cuando el modo SaaS validado está activo, visitar `/` redirige a `/mis-empresas`. Si el usuario es anónimo, el selector exigirá login de Identity. La ruta antigua `/Home/Index` sin slug sigue sin empresa validada y se deniega.
- El layout muestra **Mis empresas** y oculta el menú y botón comerciales si no hay empresa validada. Al tener empresa seleccionada, los enlaces principales incluyen `empresaSlug`, y las opciones del menú / dashboard se muestran en función del rol (seguridad visual solamente; el servidor conserva el enforcement).
- En Development normal, con flag OFF, la interfaz y el contexto mononegocio funcionan igual que antes. **No se cambió SQL Server ni hay migración de fase 6.**

## Archivos

- `Controllers/EmpresasAccesoController.cs`
- `ViewModels/EmpresasAccesoViewModel.cs`
- `Views/EmpresasAcceso/Index.cshtml`
- `Security/SlugEmpresaSeguro.cs`
- `Services/Tenancy/ValidadorAccesoEmpresaSql.cs`
- `Security/FiltroPermisosComerciales.cs`
- `Program.cs`
- `Views/Shared/_Layout.cshtml`
- `Views/Home/Index.cshtml`
- `wwwroot/css/site.css`
- `tests/Nexora.Security.Checks/Program.cs` (objetivo: **53** comprobaciones)

## Comprobaciones sin riesgo en tu equipo

Desde la carpeta que contiene `SistemaFacturacion.csproj`:

```powershell
git fetch origin
git switch feature/nexora-security-phase6-company-selector
git pull origin feature/nexora-security-phase6-company-selector
dotnet build SistemaFacturacion.csproj
dotnet run --project tests/Nexora.Security.Checks/Nexora.Security.Checks.csproj
```

**Esperado si todas pasan:** `Pruebas: 53; errores: 0`. El asistente no las ejecutó en tu SQL ni Visual Studio.

Con la bandera de tenancy **OFF**:
1. Ejecuta `F5` como siempre en tu base de desarrollo local.
2. La raíz y las rutas anteriores siguen funcionando como antes; no se muestra el selector.
3. `/mis-empresas` responde 404 y no proporciona datos de usuarios/empresas.
4. No ejecutar `Update-Database` por esta fase.

## Pruebas de seguridad de integración (solo laboratorio SQL aislado)

La bandera ON aún **no** se debe activar sobre la base habitual. Requiere primero aplicar las migraciones de fases 4 y 5 en una BD de laboratorio respaldada y configurar usuarios Identity ficticios con correos confirmados, empresas A/B Activa y membresías Activo. No habilitar `nexora-legado` ni inventar membresías en los datos normales.

1. Con flag ON, abrir `/` (redirige a `/mis-empresas`) y como anónimo seguir el Challenge a la página de login.
2. Usuario de prueba A con membresía Activo en empresa A: la pantalla solo lista A. No listar empresa B.
3. Usuario A con rol Cajero, Administrador o Propietario: la tarjeta lleva a `/e/empresa-a/Home/Index`, donde la membresía **se verifica nuevamente**. Los permisos del menú cambian según rol.
4. Usuario A no puede abrir `/e/empresa-b/Home/Index` o consultar empleados de B sin membresía activa de B.
5. Suspender membresía A y recargar: el selector ya no incluye A y las rutas `/e/empresa-a/... ` se deniegan aunque conserve una cookie Identity anterior.
6. Suspender empresa A: tampoco aparece ni se puede visitar.
7. Cuenta Identity sin empresa activa: ver mensaje de ausencia; **no crear empresa automática**.
8. Cambiar de empresa desde el menú hacia `/mis-empresas`; confirmar que la URL de la siguiente operación usa el slug correcto.
9. Entrar con propietario y navegar a **Mi equipo → Invitaciones**, verificar filtros; cajero no puede ver esas pantallas aunque construya la URL manualmente.
10. Probar explícitamente dashboard → facturas → detalle → volver, categorías, productos, posventa, reportes y redirecciones POST, porque algunas vistas comerciales heredadas aún requieren una auditoría completa de propagación del slug. Si un formulario pierde el slug, **debe denegarse**, no tomar la empresa por defecto.
11. Confirma que no cambió ningún registro de `AspNetUsers`, `EmpresaUsuarios`, empresas, stock, factura ni reembolso por navegar.
12. Revisa 404/403/redirección a login, visualización móvil y navegador privado. No compartir capturas con enlaces de invitación/token.

## Trabajo pendiente antes de producción

- Auditoría completa de todas las vistas, redirects, formularios y links para preservar slug.
- Correos transaccionales reales (invitación/confirmación) y reglas de registros Identity.
- Flujo seguro de registro inicial de empresa/propietario sin modificar BD legada.
- Tests HTTP/SQL A/B y pruebas de concurrencia; CSRF, reporte/impresión, IDOR en endpoints, rate limiting y revocación de sesiones.
- Completar y verificar las migraciones del laboratorio de fases 4–5; no usar el modo validated sobre `nexora-legado` ni en público.
- Evitar exponer la base normal de Development. Mantener PR DRAFT.

**No se ha activado el multi-tenant en el entorno del usuario; esta fase prepara su navegación en un laboratorio controlado.**

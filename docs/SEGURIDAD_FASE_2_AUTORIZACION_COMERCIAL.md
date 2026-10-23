# Nexora SaaS — Seguridad fase 2: políticas sobre módulos comerciales (opt-in)

**Rama:** `feature/nexora-security-phase2` (basada en `feature/nexora-security-phase1`; ésta a su vez se basa en `feature/nexora-ui`).  
**Estado:** código preparado y revisado estáticamente; falta compilar y probar solicitudes HTTP/SQL en Visual Studio. **NO es apto para producción**.

## Objetivo de esta fase

Aplicar el modelo de permisos de la fase 1 a **acciones comerciales MVC reales**, sin que un botón oculto en Razor sea la única barrera de seguridad. Para reducir el riesgo a la aplicación actual, todo queda **completamente desactivado por defecto**.

### Activación y comportamiento

- Sin flag `Seguridad:ActivarTenancyValidada` (valor por defecto `false`): **no se registra el filtro comercial ni la ruta empresarial**. El Development mononegocio sigue trabajando como antes, sin cambiar las operaciones de ventas, stock, devoluciones, reportes o clientes.
- Con flag `true`, **solo en un laboratorio aislado**:
  1. Identity autentica al usuario mediante `UseAuthentication()`.
  2. El middleware lee `empresaSlug` de la **ruta**, no de headers o formularios, y valida la membresía activa y empresa Activa en SQL Server.
  3. Un contexto empresarial `scoped` toma únicamente la empresa y rol verificados; si la comprobación falla, continúa vacío.
  4. `FiltroPermisosComerciales` ejecuta una política de ASP.NET Core Authorization **en cada acción MVC**. No usa `User.IsInRole` como rol empresarial.
  5. `ReglasAccesoControladores` tiene una lista cerrada de pares controlador/acción/método HTTP. **Toda acción, controlador y método no listados se deniegan**. Anónimo recibe Challenge; usuario sin permiso recibe Forbid.
- Ruta habilitada solo con flag: `/e/{empresaSlug}/{controller}/{action}/{id?}`.
- La ruta convencional `/Facturas/...` **NO lleva empresa**. Con flag activa, el filtro la rechaza aunque el usuario esté autenticado; no hay fallback a la empresa inicial.
- Identity Razor Pages continúan fuera del filtro MVC y requieren revisión específica antes de exponer el sistema; el registro público de identidades no concede membresía comercial.
- Las vistas del proyecto todavía necesitan auditoría de navegación con slug, manejo de redirects, mensajes de acceso denegado, URL generation y selector de empresa. **No se declara que todo el sitio navegue correctamente con la flag activa.**

## Tabla inicial de mapeo de acciones

| Controlador | Acción + método | Permiso |
| --- | --- | --- |
| Home | Index, Privacy, Error / GET | ConsultarOperaciones |
| Facturas | Index, Details / GET | ConsultarOperaciones |
| Facturas | Create / GET y POST | CrearFacturas |
| Facturas | Anular / POST | AnularFacturas |
| Clientes | Index, Details / GET | ConsultarOperaciones |
| Clientes | Create, Edit, Delete / GET y POST | AdministrarClientes |
| Productos, Categorias | Index, Details / GET | ConsultarOperaciones |
| Productos, Categorias | Create, Edit, Delete / GET y POST | AdministrarInventario |
| Posventa | Index, Details / GET | ConsultarPosventa |
| Posventa | Create / GET y POST | RegistrarPosventa |
| Posventa | CambiarEstado / POST | ResolverPosventa |
| Recepciones | Index, Details / GET | ConsultarRecepciones |
| ControlAnulaciones | Index / GET | ConsultarReembolsos |
| Reportes | Index / GET | ConsultarReportes |
| SeguridadEmpresa | Estado / GET | ConsultarOperaciones + policy declarada en atributo |

El mapeo `Delete` cubre las acciones POST que ASP.NET MVC expone como `Delete` mediante `[ActionName("Delete")]`, aunque el método C# interno se llame `DeleteConfirmed`.

No existe un endpoint autorizado para `ConfirmarReembolsos`, `ConfirmarRecepciones` o `LiberarInventario`. Tener esas constantes en el registro de políticas **no crea ni autoriza** una acción que mueva dinero o mercancía.

## Nuevos archivos

- `Security/ReglasAccesoControladores.cs`: mapeo explícito denegador por defecto.
- `Security/FiltroPermisosComerciales.cs`: filtro MVC `IAsyncAuthorizationFilter` que comprueba la policy mediante `IAuthorizationService` antes de ejecutar acciones.
- `Security/PermisosNexora.cs`: más permisos (AdministrarClientes, ConsultarRecepciones, RegistrarPosventa, ResolverPosventa) y matriz por roles actualizada.
- `Program.cs`: agrega filtro y rutas empresa **únicamente cuando el modo validado está activo**.
- `tests/Nexora.Security.Checks/Program.cs`: ampliación de 12 a **23 casos preparados**. Sin nuevas dependencias NuGet.

**No cambiamos controladores comerciales, vistas comerciales, relaciones de EF Core, migraciones, facturas o inventario.**

## Pruebas primero: modo normal

```powershell
git fetch origin
git switch feature/nexora-security-phase2
git pull origin feature/nexora-security-phase2
dotnet build SistemaFacturacion.csproj
dotnet run --project tests/Nexora.Security.Checks/Nexora.Security.Checks.csproj
```

Con `Seguridad:ActivarTenancyValidada=false` o sin configurar, abrir Nexora normalmente en Development y probar:
- Clientes, Productos, Categorias.
- Crear/anular factura y comprobar que solo genera reembolso y recepciones pendientes, sin restaurar stock automáticamente.
- Reportes, Posventa, Recepciones (solo simulación), Centro de control de anulaciones.
- Inicio/cierre de sesión Identity. Los flujos comerciales Development **aún están en modo legado sin enforcement de permisos**; no confundir esta prueba de regresión con seguridad multiempresa.

**No ejecutar `Update-Database`: no hay migraciones nuevas.**

## Pruebas después: modo SaaS validado, SOLO EN BASE AISLADA

No tocar la empresa heredada ni crear empresas de prueba en la base habitual.

1. Crear un laboratorio SQL separado, migrado y respaldado, con empresas A/B y usuarios Identity A/B de prueba, con membresías verificables y estados Activa/Activo.
2. Activar flag `Seguridad:ActivarTenancyValidada=true` mediante User Secrets o variable de entorno **solo en el laboratorio**.
3. Usuario anónimo en `/e/empresa-a/Facturas/Index` → Challenge (login).
4. Usuario autenticado sin membresía activa en esa empresa → Forbid.
5. Cajero de empresa A: GET `/e/empresa-a/Facturas/Index` → permitido; POST `/e/empresa-a/Facturas/Anular/{id}` → Forbid, incluso si construye el POST manualmente.
6. Cajero intentando POST `/e/empresa-a/Productos/Edit/{id}` → Forbid.
7. Administrador de A: GET `/e/empresa-a/Recepciones/Index` → permitido, **sin confirmar recepción**.
8. Usuario A intenta `/e/empresa-b/Facturas/Index` sin membresía B → Forbid, nunca datos de A.
9. Empresa suspendida, membresía suspendida, rol inválido → denegado.
10. `/Facturas/Index` sin slug con la flag activa → Forbid incluso para usuario autenticado, pues no hay empresa validada.
11. GET a una acción POST (por ejemplo `Anular`) o método HTTP no previsto → denegado o 405 por routing, nunca ejecución sensible.
12. Comparar cifras, impresión y reportes solo dentro de cada empresa; comprobar que navegación/redirects preservan la ruta tenant donde aplique.

**Importante:** se deberá completar el selector empresarial y la propagación de slug en vínculos/redirects antes de tener una UI SaaS navegable con seguridad. Este PR prepara el enforcement, no resuelve todavía la navegación completa.

## Riesgos y pendientes

- [ ] Compilar .NET 8, ejecutar 23 verificaciones y pruebas HTTP de esta fase.
- [ ] Verificar permisos por acción en integración con SQL Server real, no solo usando mocks.
- [ ] Revisar autenticación y registro de usuario Identity; definir alta por invitación sin autorregistro empresarial.
- [ ] Selector de empresa y propagación segura de rutas/redirects.
- [ ] Política de acceso a archivos, exportaciones, PDFs/impresión, websockets y futuros APIs.
- [ ] Pruebas de `[ActionName]`, servicios, validaciones antiforgery y concurrencia.
- [ ] Gestión real de usuarios/roles; bloqueo de escalada, último propietario y auditoría.
- [ ] Retiro de defaults legados de `IdEmpresa` antes de activar tenants.
- [ ] Confirmaciones reales de recepción, stock y reembolso **bloqueadas** hasta auditoría, comprobantes y transacciones idempotentes.

**No integrar con `master` ni desplegar públicamente. Mantener el PR en borrador hasta cerrar estas verificaciones.**

# Nexora — fase 8: navegación segura y sesiones reales de Identity

**Rama:** `feature/nexora-security-phase8-navigation` (basada en fase 7).  
**Estado:** implementación revisada estáticamente; todavía falta compilar y probar con navegador en el equipo del desarrollador. **No habilitar SaaS en producción.**

## Qué mejora esta fase

1. `PreservarRutaEmpresaFiltro` agrega `empresaSlug` a los resultados `RedirectToAction` de una petición empresarial ya validada. Sobrescribe cualquier slug distinto puesto en los valores de redirección; si el contexto no es fiable, no fabrica una empresa. Solo se registra con `Seguridad:ActivarTenancyValidada=true`. No interviene en las redirecciones públicas a `EmpresasAcceso` o `Invitaciones`.
2. Se incorporó `asp-route-empresaSlug` explícito a enlaces y formularios de **Clientes, Categorías, Productos, Facturas, Posventa, Recepciones, Reportes y Control de Anulaciones**. Con flag OFF, el slug es nulo y permanece el flujo Development mononegocio.
3. Se añaden **5 comprobaciones** al runner de seguridad, pasando de 53 a **58 casos preparados**, sobre redirecciones y aislamiento de rutas.
4. Se incluye `tests/Nexora.Browser.Lab`: pequeño ejecutable **solo de laboratorio** para preparar cuentas reales de ASP.NET Identity con contraseñas aleatorias, empresas y roles ficticios, con opción de limpieza. No crea bases, no instala migraciones, no usa secretos fijos ni permite conectarse a la base habitual.
5. **No cambian tablas, migraciones, controladores de facturación, stock ni reglas de dinero.** Los 21 casos SQL de fase 7 siguen siendo independientes. Objetivo combinado: 58 lógicos + 21 SQL, y luego validaciones manuales con navegador/cookies reales.

## Limitaciones de seguridad

- Las cuentas de prueba se marcan `EmailConfirmed=true` **directamente en el fixture local y descartable**, exclusivamente para verificar login. Esto NO valida el envío ni confirmación real de correos del onboarding.
- La suite de navegación **no automatiza clicks con navegador**, requiere seguir la lista manual. No se ha demostrado todavía que todas las rutas y formularios de producción sean correctos.
- No se ha auditado aún cada enlace generado desde JavaScript, impresiones/exportaciones, detalles por ID, APIs o conexiones de terceros.
- Las operaciones de reembolso real y recepción física siguen deshabilitadas. No crear nuevas empresas en la base normal ni usar usuarios reales.
- El preparador de navegador **deja las cuentas ficticias hasta que se ejecuta `--cleanup`**. Después de limpiar, el runner SQL de fase 7 vuelve a encontrar el laboratorio vacío.

## 1. Primero verificar código sin tocar SQL

En PowerShell, desde la carpeta donde está `SistemaFacturacion.csproj`:

```powershell
git fetch origin
git switch feature/nexora-security-phase8-navigation
git pull origin feature/nexora-security-phase8-navigation
dotnet build SistemaFacturacion.csproj
dotnet run --project tests/Nexora.Security.Checks/Nexora.Security.Checks.csproj
```

**Resultado esperado, no confirmado aún:** `Pruebas: 58; errores: 0`. En caso de error, detenerse y compartir la salida sin secretos.

## 2. Confirmar la base DESCARTABLE y la marca

Usar **únicamente** la base ya creada `NexoraIsolationTest` en SQL Server LOCAL, donde se aplicaron las migraciones y se ejecutaron correctamente las 21 pruebas SQL de fase 7.

En SSMS (en la base correcta), consultar SIN MODIFICAR:

```sql
USE [NexoraIsolationTest];
GO
SELECT DB_NAME() AS Base, Marca
FROM dbo.NexoraIsolationLabGuard;
SELECT COUNT(*) AS CuentasExistentes FROM dbo.AspNetUsers;
SELECT COUNT(*) AS MembresiasExistentes FROM dbo.EmpresaUsuarios;
```

La marca `NEXORA_EXPLICIT_LAB_ONLY` debe existir. Las tablas de cuentas y membresías deben estar vacías al iniciar. **Si hay cuentas o ventas, NO las elimines para forzar la prueba**; revisa por qué están allí.

En PowerShell, define la conexión explícita de laboratorio. Adaptar `Server` si tu SQL local usa otra instancia, **pero nunca cambiar Database=NexoraIsolationTest**:

```powershell
$env:NEXORA_ISOLATION_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=NexoraIsolationTest;Trusted_Connection=True;TrustServerCertificate=True'
```

## 3. Crear temporalmente cuentas reales de Identity

```powershell
dotnet run --project tests/Nexora.Browser.Lab/Nexora.Browser.Lab.csproj -- --prepare
```

El programa aborta si el servidor no es local, el nombre de DB no es exacto, falta la marca manual o hay datos previos. Si todo está preparado, crea en una sola transacción:

| Usuario ficticio | Empresa / rol |
| --- | --- |
| `owner-a@browser.nexora.invalid` | `lab-browser-a` / Propietario |
| `cashier-a@browser.nexora.invalid` | `lab-browser-a` / Cajero |
| `owner-b@browser.nexora.invalid` | `lab-browser-b` / Propietario |
| `no-member@browser.nexora.invalid` | Sin membresías |

Muestra las **contraseñas aleatorias UNA SOLA VEZ** en tu consola. Guárdalas temporalmente en un sitio local seguro solo para esta prueba. **No envíes capturas ni copies contraseñas al chat, correo o GitHub.** No son cuentas reales ni deben ser utilizables fuera del laboratorio.

## 4. Ejecutar una SEGUNDA instancia de la WEB conectada al laboratorio

Abre **otra ventana PowerShell** en la carpeta Nexora. Comprueba la cadena y escribe lo siguiente en esa ventana (no cambiar `appsettings.json` ni User Secrets habituales):

```powershell
$env:NEXORA_ISOLATION_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=NexoraIsolationTest;Trusted_Connection=True;TrustServerCertificate=True'
$env:ConnectionStrings__DefaultConnection = $env:NEXORA_ISOLATION_TEST_CONNECTION
$env:Seguridad__ActivarTenancyValidada = 'true'
dotnet run --project SistemaFacturacion.csproj --launch-profile https
```

- El perfil HTTPS actual anuncia `https://localhost:7131`; utiliza la URL que realmente aparezca en la consola si es distinta.
- Si el certificado local de desarrollo falla, consultar el error y configurar certificados de desarrollo. **No usar un despliegue público HTTP para el laboratorio.**
- Esta bandera y el override de cadena se aplican **solo a la ventana PowerShell**. Al cerrarla, desaparece la configuración. La base habitual permanece fuera del experimento.
- Comprueba que la pantalla `https://localhost:7131/` redirija a `/mis-empresas`. Un anónimo debe acabar en login Identity. Puedes abrir directamente `https://localhost:7131/Identity/Account/Login`.

## 5. Pruebas con cookies reales, una sesión por rol

Cierra sesión entre usuarios o utiliza **perfiles separados** de navegador (una ventana incógnita no siempre crea sesión independiente si hay otras incógnitas abiertas).

1. Como `owner-a`: inicia sesión mediante el formulario Identity; `/mis-empresas` muestra solamente `lab-browser-a`. Entra y comprueba que la barra de direcciones empieza con `/e/lab-browser-a/`.
2. Desde A, abre **Clientes → Productos → Facturas → Posventa → Reportes → Mi equipo**. Si aparece un 403/404 o un enlace pierde `/e/lab-browser-a/`, anota la ruta y detén esa parte; no desactives permisos.
3. Desde A, visita directamente `/e/lab-browser-b/Equipo/Index` o `/e/lab-browser-b/Facturas/Index`: debe devolver acceso denegado/redirigir a AccessDenied, nunca mostrar registros de B.
4. Como `cashier-a`: selector muestra A; crear factura GET está permitido, pero **Mi equipo** y Control de Anulaciones están denegados. Debe poder consultar sus pantallas permitidas; no pruebes operaciones POST que modifiquen stock.
5. Como `owner-b`: selector muestra B, no A; intentos de usar rutas A deben ser rechazados.
6. Como `no-member`: login Identity exitoso, selector vacío, acceso a cualquier URL `/e/lab-browser-a/...` denegado.
7. Desde una pantalla comercial GET, inspecciona en el navegador los enlaces y la propiedad `action` de los formularios. Deben contener `/e/{slug}/...` y, para formularios POST, el token anti-CSRF `__RequestVerificationToken`. **No es todavía una prueba de POST válido/CSRF de extremo a extremo.**
8. Cuando una petición pierde el slug, **debe denegarse**, no caer en la empresa heredada. Si ves datos de otra empresa, DETÉN las pruebas y anota la URL sin enviar datos personales.
9. Probar navegación móvil, botones Atrás y cambios de sesión. No usar cuentas reales ni datos privados.

**No emitir facturas, registrar clientes ni crear invitaciones en este laboratorio de navegación.** La limpieza automática supone que solo se navegaron GET y login/logout; si decides probar operaciones de escritura, necesitarás otro fixture y una estrategia de limpieza propia.

## 6. Apagar el laboratorio y retirar las cuentas

1. Detén `dotnet run` de la aplicación web con `Ctrl+C` y cierra esa ventana PowerShell (elimina `ConnectionStrings__DefaultConnection` y la bandera SaaS activadas allí).
2. En el PowerShell original que conserva `NEXORA_ISOLATION_TEST_CONNECTION`, ejecuta:

```powershell
dotnet run --project tests/Nexora.Browser.Lab/Nexora.Browser.Lab.csproj -- --cleanup
Remove-Item Env:NEXORA_ISOLATION_TEST_CONNECTION
```

El limpiador comprueba exactamente **dos empresas, cuatro cuentas y tres membresías** de prueba y, si algo cambió, se detiene para evitar borrar otros datos. Si no puede limpiar, **no repitas comandos de eliminación manualmente**; inspecciona y comparte solo el error, sin contraseñas.

La base comercial cotidiana de Nexora **nunca debe recibir** `Seguridad:ActivarTenancyValidada=true` en esta fase.

## Próximos pasos bloqueantes

- Pruebas HTTP automatizadas con TestServer/Identity y CSRF, no solo pruebas manuales.
- Validación de autorización por entidad y rutas de impresión/exportación/JSON.
- Revisión de todos los redirects y enlaces JavaScript, acceso a archivos y antiforgery.
- Correos reales e invitaciones con onboarding seguro.
- Desactivar definitivamente defaults heredados antes de habilitar un segundo tenant real.
- Seguridad de sesiones, políticas de limitación de intentos, recuperación de cuenta y permisos mínimos de SQL.

**Mantener rama y PR en borrador. No fusionar a `master` ni considerar apto para producción.**

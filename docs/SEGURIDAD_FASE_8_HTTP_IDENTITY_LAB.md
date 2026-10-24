# Nexora SaaS — Fase 8: pruebas HTTP con cookies reales de Identity

**Rama:** `feature/nexora-security-phase8-http-lab` sobre `feature/nexora-security-phase7-sql-lab`. **Estado:** código de pruebas preparado, compilación y ejecución locales pendientes.

## Qué cambia respecto a las fases anteriores

- La fase 6 preparó selector y rutas `/e/{empresaSlug}/...`.
- La fase 7 pasó en la máquina del desarrollador: **21/21 comprobaciones SQL** con identidades simuladas, además de **53/53 reglas de seguridad**.
- Esta fase usa `WebApplicationFactory<Program>` y `TestServer` **en memoria** para ejecutar peticiones HTTP, las páginas Razor Identity y **cookies de inicio de sesión reales**.
- No abre un puerto a Internet, no usa cuentas reales, no expone contraseñas ni necesita navegador gráfico. Se comprueban status HTTP, redirecciones a login/AccessDenied y HTML generado.
- Usa `Microsoft.AspNetCore.Mvc.Testing 8.0.23` únicamente en el nuevo proyecto `tests/Nexora.Http.Lab`; el código comercial y EF permanecen sin cambios.
- Se añadió `public partial class Program { }` a la web para que TestServer use el punto de entrada verdadero y no el del ejecutable de pruebas.

## Protección contra accidentes

La suite **se niega a ejecutar antes de escribir** si falta algo de esto:

1. Variable **`NEXORA_ISOLATION_TEST_CONNECTION`** explícita; nunca usa la conexión habitual de `appsettings.json` para elegir destino.
2. SQL Server LOCAL con nombre de BD exactamente `NexoraIsolationTest`.
3. Tabla/marca creadas manualmente `NexoraIsolationLabGuard / NEXORA_EXPLICIT_LAB_ONLY`.
4. Tablas SQL esenciales existentes (incluidas `AuditoriaMembresias`).
5. Base sin usuarios Identity, membresías ni información comercial (incluidos `Clientes`, `Categorias`, `Productos`, `Facturas`, `DetallesFactura`, `Pagos`, `InvitacionesEmpresa` y `AuditoriaMembresias`).

Crea 2 usuarios Identity ficticios de correo `example.invalid` con contraseñas aleatorias **solo en memoria del runner**, correo marcado confirmado para simular la precondición. Crea 2 empresas y membresías (A Cajero, B Propietario).

Las pruebas usan 3 clientes HTTP independientes: anónimo, usuario A con cookie y usuario B con cookie. Todas las cuentas y empresas generadas por la ejecución se eliminan en `finally`. Si falla la limpieza, la suite falla y **no debe repetirse** hasta recuperar una base limpia. El runner **no** crea bases, ejecuta migraciones, envía correos ni toca `master`.

**Este es un laboratorio, no una demostración de que el registro público y el correo electrónico estén configurados para producción.**

## Preparación y ejecución (Windows)

1. Usa el SQL local y la misma BD **NexoraIsolationTest** que sirvió para las 21 comprobaciones anteriores; la ejecución anterior debe haber terminado con `LIMPIEZA OK`.
2. Abre PowerShell en la carpeta que contiene `SistemaFacturacion.csproj`.
3. Actualiza rama:
   ```powershell
   git fetch origin
   git switch feature/nexora-security-phase8-http-lab
   git pull origin feature/nexora-security-phase8-http-lab
   ```
4. Compila sitio y nuevo ejecutable **por separado** (la primera vez NuGet descargará el paquete oficial):
   ```powershell
   dotnet build SistemaFacturacion.csproj
   dotnet build tests/Nexora.Http.Lab/Nexora.Http.Lab.csproj
   ```
5. Prepara la conexión **solo en esa terminal**. Si tu servidor local fue `(localdb)\MSSQLLocalDB`, utiliza exactamente:
   ```powershell
   $env:NEXORA_ISOLATION_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=NexoraIsolationTest;Trusted_Connection=True;TrustServerCertificate=True'
   ```
   Si tu servidor es diferente, cambia `Server`, nunca `Database`. **No compartas credenciales o conexiones privadas en capturas**.
6. Ejecuta:
   ```powershell
   dotnet run --project tests/Nexora.Http.Lab/Nexora.Http.Lab.csproj
   ```
7. Objetivo si todo pasa:
   ```text
   LIMPIEZA HTTP OK: identidades y empresas ficticias retiradas.
   HTTP Identity: 16 verificaciones, 0 errores.
   ```
   Esa salida es **esperada**, aún no se ha ejecutado en el equipo del asistente.

Después, borra la variable del entorno de la terminal:

```powershell
Remove-Item Env:NEXORA_ISOLATION_TEST_CONNECTION
```

**NO actives `Seguridad:ActivarTenancyValidada` en la base cotidiana.** El ejecutable lo activa únicamente dentro de su propio TestServer, con la conexión de laboratorio.

## Qué prueban exactamente los 16 casos

- Un visitante anónimo en el selector o facturas empresariales es redirigido a login.
- Login REAL con antiforgery GET/POST de Razor Pages Identity y cookies aisladas por usuario.
- A solo ve A en `/mis-empresas`; B solo ve B.
- A abre dashboard A; B abre dashboard B.
- A no entra al dashboard de B; B no entra al dashboard de A.
- Acceso comercial sin slug sigue denegado aun con cookie autenticada.
- Cajero A consulta clientes dentro de su tenant, pero no lee el directorio de empleados.
- Propietario B puede ver su directorio sin datos del usuario A.
- POST de anulación del cajero se deniega; POST administrativo del propietario SIN antiforgery obtiene 400.
- Suspender membresía A en SQL invalida su acceso con cookie todavía existente.
- Suspender empresa B en SQL invalida la ruta de su propietario aunque siga autenticado.

**Límites:** no prueba pago/stock, registro de nuevas cuentas, confirmación real de correo, ni todos los enlaces/redirects comerciales. Tampoco reproduce todavía un navegador gráfico ni evidencia de correos externos. La lógica de datos cruzados de EF ya fue probada en la fase 7 y este runner se centra en HTTP/cookies.

## Si falla algo

- Error de compilación del paquete Testing: comprobar conexión de NuGet y SDK .NET 8 instalado. No agregar versiones incompatibles del paquete.
- `STOP SIN ESCRIBIR DATOS`: revisar BD, marca, connection string y registros previos; **no quitar guards**.
- Fallo HTTP: guardar solo mensajes y status codes sin contraseñas/tokens. La suite debería limpiar su fixture; confirmar `LIMPIEZA HTTP OK`.
- Si aparece `ALERTA: limpieza HTTP falló`, **detenerse**. La siguiente ejecución está bloqueada deliberadamente porque ya no queda una base vacía.
- No aplicar `Update-Database`: esta fase NO tiene nuevas migraciones.

**Mantener PR en borrador, sin merge a master ni activación de empresa B en BD real.**

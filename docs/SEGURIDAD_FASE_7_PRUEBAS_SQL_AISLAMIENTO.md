# Nexora — Fase 7: pruebas reales de aislamiento en SQL Server local

**Rama:** `feature/nexora-security-phase7-sql-lab` encima de fase 6.  
**Estado:** entregable preparado para revisión. **No ejecutamos SQL Server ni dotnet en el equipo del asistente.**

## Objetivo y límites

Las **53 verificaciones** de `Nexora.Security.Checks` prueban reglas en memoria. La nueva suite `Nexora.Integration.Lab` agrega **21 comprobaciones con SQL Server real** sobre filtros EF, `ValidadorAccesoEmpresaSql`, middleware, permisos por rol y revocación de membresías. Son **74 verificaciones previstas** entre ambos ejecutables, **no resultados confirmados**.

Esto **NO prueba todavía** cookies reales, login Razor Pages, antiforgery del navegador, peticiones HTTP, redirecciones, creación completa de facturas, pruebas de concurrencia ni todos los flujos SaaS. Es una capa de pruebas **SQL + componentes backend con usuario ficticio y ClaimsPrincipal simulado**. No presentar como prueba end-to-end HTTP.

## Bloqueos de seguridad deliberados

El ejecutable **se detiene antes de escribir** salvo que se cumplan TODAS las siguientes condiciones:

1. Variable de entorno `NEXORA_ISOLATION_TEST_CONNECTION` definida explícitamente. No toma la cadena habitual `DefaultConnection`.
2. Servidor SQL **local**: `(localdb)\...`, `.\...`, `localhost`, `127.0.0.1`, etc. No se aceptan servidores remotos.
3. Nombre exacto de base de datos: **`NexoraIsolationTest`**.
4. La base ya existe, tiene las migraciones de Nexora y una tabla de autorización manual `dbo.NexoraIsolationLabGuard` con la marca `NEXORA_EXPLICIT_LAB_ONLY`.
5. **No contiene usuarios, membresías ni filas comerciales anteriores** (`AspNetUsers`, `EmpresaUsuarios`, `Clientes`, `Categorias`, `Productos`, `Facturas`, `DetallesFactura`, `Pagos`).
6. El ejecutable **NO crea ni actualiza esquemas** ni llama a `MigrateAsync`. Las migraciones se deben aplicar antes y manualmente SOLO en ese laboratorio.

Los datos falsos se identifican mediante GUID/etiquetas aleatorias por ejecución y se borran en un bloque `finally`. Si la limpieza falla, el resultado general **falla** y debes revisar el laboratorio. No se borran datos de terceros ni se permite usar una base con información real. **No uses la base normal de Nexora.**

## Preparación local paso a paso (Windows)

### 1. Descargar y comprobar código

Abrir PowerShell en la carpeta que contiene `SistemaFacturacion.csproj`:

```powershell
git fetch origin
git switch feature/nexora-security-phase7-sql-lab
git pull origin feature/nexora-security-phase7-sql-lab
dotnet build SistemaFacturacion.csproj
dotnet run --project tests/Nexora.Security.Checks/Nexora.Security.Checks.csproj
```

Esperado, si todo está correcto: `Pruebas: 53; errores: 0`. Si compilar o estas pruebas falla, **detenerse**.

### 2. Crear una base local realmente separada

En SQL Server Management Studio (SSMS) o en las herramientas SQL de Visual Studio, conéctate a tu SQL Server **local**, por ejemplo `(localdb)\MSSQLLocalDB` si es donde trabajas.

Crea **una nueva base vacía llamada exactamente** `NexoraIsolationTest`. Verifica que no estás seleccionando la base habitual de Nexora. **No clones registros de clientes ni facturas.**

En PowerShell, define la conexión *únicamente para esa terminal*. Este ejemplo usa autenticación integrada de Windows y LocalDB; si tu SQL Server local es otro, cambia `Server`, pero conserva `Database=NexoraIsolationTest`:

```powershell
$env:NEXORA_ISOLATION_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=NexoraIsolationTest;Trusted_Connection=True;TrustServerCertificate=True'
```

Comprueba visualmente que dice `NexoraIsolationTest`, no tu base comercial. No compartas cadenas con contraseñas en capturas.

### 3. Migrar SOLO ese laboratorio

Las fases 4 y 5 tenían migraciones necesarias para las invitaciones; deben existir también en este laboratorio. Ejemplo con `dotnet ef` si lo tienes instalado:

```powershell
dotnet ef database update --connection "$env:NEXORA_ISOLATION_TEST_CONNECTION" --project SistemaFacturacion.csproj --startup-project SistemaFacturacion.csproj
```

Si `dotnet ef` no está disponible, puedes usar **Package Manager Console** de Visual Studio y `Update-Database -Connection "cadena_de_conexion_DEL_LABORATORIO"`, verificando antes que `NexoraIsolationTest` es la base elegida. Si hay cualquier duda o error de migración, **detente y comparte el error antes de continuar**.

La suite NO ejecuta `Update-Database` automáticamente y no modifica `appsettings.json` ni User Secrets habituales.

### 4. Crear la marca de autorización manual

En SSMS o Visual Studio, con la base `NexoraIsolationTest` explícitamente seleccionada, ejecuta el script:

`tests/Nexora.Integration.Lab/VALIDAR_LAB.sql`

El script comprueba `DB_NAME() = 'NexoraIsolationTest'` antes de crear su tabla y marca. Sin ella, el ejecutable se negará a correr.

### 5. Ejecutar la suite SQL

Desde la misma terminal PowerShell donde quedó definida la variable de entorno:

```powershell
dotnet run --project tests/Nexora.Integration.Lab/Nexora.Integration.Lab.csproj
```

**Resultado esperado (si se verifica todo):**

```text
LIMPIEZA OK: se retiraron los datos ficticios de esta ejecución.
Integración SQL: 21 verificaciones, 0 errores.
```

Un error de preflight con prefijo `STOP SIN ESCRIBIR DATOS` significa que el programa **no insertó usuarios ni empresas**. No intentes deshabilitar la protección para ejecutarlo. Revisa conexión y base elegida.

### 6. Limpiar la variable de entorno

```powershell
Remove-Item Env:NEXORA_ISOLATION_TEST_CONNECTION
```

No actives todavía `Seguridad:ActivarTenancyValidada` en tu instalación normal.

## Qué comprueba realmente la suite de 21 casos

- Un anónimo no tiene membresías válidas.
- Usuario A/Cajero pertenece a Empresa A; usuario B/Propietario pertenece a Empresa B.
- A no puede entrar en B ni B en A; tampoco en slug inexistente.
- Middleware solo establece el contexto con una membresía SQL validada.
- Cajero A puede crear facturas según su política, pero **no** anularlas ni administrar empleados.
- Propietario B puede administrar su equipo.
- El query-filter EF para `Clientes` solo muestra datos de su propio tenant.
- El contexto `Guid.Empty` no ve ni escribe datos comerciales.
- Insertar un cliente de B desde un contexto A es rechazado por `SaveChanges`.
- Suspensión y baja de la membresía A bloquean la validación SQL siguiente.
- Suspensión de empresa A bloquea A sin afectar B.
- La consulta global de membresías se filtra explícitamente por la empresa esperada.

**Estos datos de ejemplo no permiten login real:** los IdentityUser ficticios carecen de contraseña y se usan solo para probar resolución de membresía mediante claims de prueba. No se envían correos ni se da de alta una empresa comercial real.

## Pendientes antes de producción

- HTTP real con cookies Identity, selector de empresa, rutas y redirects.
- Probar privacidad de vistas, impresión, PDF, reportes y servicios que usan SQL directo.
- Migrar y luego retirar defaults de empresa heredada antes de soportar un segundo tenant real.
- Registrar propietario inicial y correos confirmados mediante flujos seguros de onboarding.
- Tests de concurrencia de invitación/aceptación/revocación, antiforgery y autorización por entidad.
- Observabilidad, pruebas de recuperación, auditoría de permisos SQL y seguridad de sesiones.

**Mantener PR en borrador, modo validado OFF y `master` sin fusionar.**

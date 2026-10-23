# Nexora SaaS — Seguridad fase 1: autenticación, contexto validado y permisos

**Rama:** `feature/nexora-security-phase1` (independiente de `feature/nexora-ui`).  
**Estado:** preparado para revisión; **no compilado ni ejecutado con SQL Server en el entorno del asistente**.

## Decisiones y límites

Esta fase incorpora las piezas técnicas necesarias para que Nexora pueda validar identidad y membresía por empresa antes de autorizar acciones comerciales:

1. Se declara `app.UseAuthentication()` explícitamente antes de `UseAuthorization()`.
2. `ContextoEmpresaVerificado` comienza con `Guid.Empty`, sin rol; únicamente el middleware de servidor puede asignar los valores devueltos por `IValidadorAccesoEmpresa.ValidarAsync`, que consulta SQL Server y no `ApplicationDbContext`.
3. `VerificacionMembresiaMiddleware` solo toma `empresaSlug` de la **ruta seleccionada**. El slug es un identificador candidato, no un permiso. El validador SQL comprueba usuario autenticado, empresa Activa, membresía Activa/no dada de baja y rol permitido.
4. `PermisosNexora`, `PermisoEmpresaRequirement` y `PermisoEmpresaHandler` preparan autorización por empresa + operación. El permiso no se toma de `User.IsInRole`, ni de un form, query, header o claim de rol arbitrario.
5. Único endpoint nuevo: **GET `/e/{empresaSlug}/seguridad/estado`**. Exige `Nexora.Comercial.Consultar` y devuelve únicamente la empresa y rol verificados. Es un **diagnóstico**, no un módulo administrativo.
6. Fuera de Development, la ruta convencional MVC requiere autenticación. Esto **no convierte Nexora en un SaaS seguro todavía**: las rutas atribuidas y toda autorización comercial granular requieren auditoría individual. No exponer producción ni activar múltiples empresas.
7. La fase **no cambia** `Factura`, `Pago`, `Reembolso`, `RecepcionAnulacion`, `Producto`, inventario ni las migraciones.

## Activación deliberadamente controlada

`Program.cs` lee la bandera:

```text
Seguridad:ActivarTenancyValidada
```

- **Ausente o `false` (por defecto):** en Development se mantiene `ContextoEmpresaDesarrollo` para no romper el flujo mononegocio actual. Fuera de Development el mismo contexto deja `Guid.Empty` y las consultas comerciales siguen fallando cerradas. La ruta nueva de diagnóstico queda denegada incluso con Identity autenticado.
- **`true` (solo laboratorio aislado):** `IContextoEmpresa` utiliza `ContextoEmpresaVerificado` (scoped); se ejecuta el middleware de membresías después de autenticar. **Las rutas comerciales convencionales todavía no llevan `empresaSlug` ni permisos por acción**, así que no pueden utilizar este modo para vender o acceder a otro tenant. No es un interruptor para activar el SaaS.

No cambiar la empresa heredada `nexora-legado` a `Activa` ni crear membresías manuales en la base normal. La prueba integrada real requiere **base aislada** con una empresa y usuario de prueba, sin alterar datos habituales.

## Mapa de permisos inicial

| Permiso | Propietario | Administrador | Cajero |
| --- | --- | --- | --- |
| Consulta comercial | Sí | Sí | Sí |
| Crear facturas | Sí | Sí | Sí |
| Anular facturas | Sí | Sí | No |
| Administrar inventario | Sí | Sí | No |
| Consulta posventa | Sí | Sí | Sí |
| Consulta reembolsos | Sí | Sí | No |
| Consulta reportes | Sí | Sí | No |
| Administrar usuarios | Sí | No | No |
| Administrar roles | Sí | No | No |
| Administrar empresa | Sí | No | No |
| Confirmar recepciones físicas | Sí (futuro) | No | No |
| Liberar mercancía al inventario | Sí (futuro) | No | No |
| Confirmar reembolsos | Sí (futuro) | No | No |

La matriz representa el **contrato en código**, no permisos activos sobre operaciones financieras. No se habilitó ningún endpoint para confirmar recepciones, liberar mercancía ni devolver dinero. Los controles de esas operaciones requieren auditoría, idempotencia, evidencia y pruebas de concurrencia adicionales.

## Archivos entregados

- `Program.cs`: DI de validador, política y handler, UseAuthentication y bandera explícita.
- `Services/Tenancy/ContextoEmpresaVerificado.cs`: contexto scoped e inmutable tras la primera validación.
- `Services/Tenancy/VerificacionMembresiaMiddleware.cs`: validación por ruta cuando se activa el modo de laboratorio.
- `Security/PermisosNexora.cs`: matriz por rol empresarial.
- `Security/PermisoEmpresaRequirement.cs` y `Security/PermisoEmpresaHandler.cs`: integración con autorización ASP.NET Core.
- `Controllers/SeguridadEmpresaController.cs`: diagnóstico GET con política requerida.
- `tests/Nexora.Security.Checks/`: runner autónomo **sin paquetes nuevos**; se excluye del build de la web mediante `<Compile Remove="tests/**/*.cs" />` en el .csproj principal.

## Verificación desde Visual Studio

Primero mantener un respaldo y probar en la base habitual de **Development local**, sin activar el nuevo modo:

```powershell
git fetch origin
git switch feature/nexora-security-phase1
git pull origin feature/nexora-security-phase1
dotnet build SistemaFacturacion.csproj
dotnet run --project tests/Nexora.Security.Checks/Nexora.Security.Checks.csproj
```

Resultado esperado del runner: pruebas de permisos, sesión anónima, sin slug, membresía denegada, rol por petición, rechazo de cambio de empresa y Guid.Empty. El proceso debe salir con código 0.

Luego ejecutar la web como normalmente y verificar regresiones: Clientes, Facturas, Productos, Inventario, Posventa, Reembolsos pendientes, Recepciones y Centro de control deben comportarse exactamente igual en Development. **No ejecutar `Update-Database`: no se añadió migración.**

Para el diagnóstico aislado, realizarlo **solo en una copia de prueba**:

1. Crear o preparar una empresa de prueba `Activa`, una membresía de prueba `Activo` con `FechaBajaUtc = NULL`, y un usuario Identity de prueba con sesión válida. No tocar `nexora-legado`.
2. Activar temporalmente `Seguridad:ActivarTenancyValidada=true` mediante **User Secrets**, variables de entorno o configuración no versionada. No publicar claves o datos privados.
3. Abrir `/e/{slug-de-prueba}/seguridad/estado`. Con cookie autenticada y membresía válida devuelve empresa/rol verificados.
4. Usuario anónimo, empresa distinta, membresía suspendida, empresa suspendida, rol inválido o petición sin slug deben denegarse.
5. Desactivar la bandera y comprobar nuevamente el desarrollo mononegocio.

**No utilizar la bandera para pruebas comerciales normales**: al activarla, los controladores comerciales convencionales todavía no tienen el slug y fallarán cerrados por diseño.

## Próximas fases bloqueantes

- [ ] Confirmar compilación .NET 8 y resultados del runner.
- [ ] Tests SQL reales con usuario A/empresa A y usuario B/empresa B, suspensión, revocación y accesos cruzados.
- [ ] Diseñar selector de empresa autenticado con anti-CSRF y conservar selección sin confiar en el navegador.
- [ ] Aplicar autorización granular real a todos los controladores existentes, no únicamente al diagnóstico.
- [ ] Diseñar política de alta: invitaciones, confirmación de correo y propietario inicial; evitar registro anónimo indiscriminado.
- [ ] Proteger endpoints que usan SQL directo, `ExecuteUpdate`, `FindAsync`, descargas, impresión y reportes.
- [ ] Retirar defaults heredados de `IdEmpresa` con migración revisada antes de un segundo tenant.
- [ ] Incorporar auditoría de acciones sensibles y protección del último propietario.
- [ ] Diseñar y probar confirmación de recepción/reembolso con roles, pruebas concurrentes e idempotencia.

**Nunca anunciar esta fase como seguridad SaaS lista para producción. No fusionar a `master`, no activar segunda empresa ni conceder permisos comerciales reales todavía.**

# NEXORA — Fase 8.5: validación de autorización empresarial

Estado: **plan documentado; pruebas de roles pendientes**. No habilitar `Seguridad:ActivarTenancyValidada` en la base de datos cotidiana hasta completar el laboratorio aislado y sus migraciones/fixtures.

## Lo que ya está implementado
- `Security/PermisosNexora.cs`: matriz explícita de Propietario, Administrador y Cajero.
- `Security/ReglasAccesoControladores.cs`: asigna permisos por controlador, acción y verbo HTTP. Desconocidos: denegar.
- `Security/FiltroPermisosComerciales.cs`: verifica permisos cuando el flag está habilitado.
- `Services/Tenancy/VerificacionMembresiaMiddleware.cs`: obtiene la empresa del slug en ruta, y valida la membresía contra SQL.
- `Security/PermisoEmpresaHandler.cs`: exige sesión + empresa validada + permiso del rol.

## Matriz mínima para el laboratorio (modo SaaS validado)

| Operación | Propietario | Administrador | Cajero |
|---|---|---|---|
| Consultar productos, clientes y facturas (GET) | Permitir | Permitir | Permitir |
| Crear factura (GET/POST) | Permitir | Permitir | Permitir |
| Crear/editar cliente (GET/POST) | Permitir | Permitir | Denegar |
| Crear/editar producto (GET/POST) | Permitir | Permitir | Denegar |
| Anular factura (POST) | Permitir | Permitir | Denegar |
| Consultar reportes (GET) | Permitir | Permitir | Denegar |
| Administrar equipo y roles | Permitir | Denegar | Denegar |

Los resultados anteriores son **esperados por la matriz de código**, no resultados de pruebas de ejecución.

## Preparación obligatoria
1. Base SQL separada, con empresa A y empresa B y datos comerciales aislados.
2. Tres cuentas propias de prueba: una por rol en empresa A y una cuarta cuenta sin membresía.
3. Opcional: membresía de la misma cuenta con rol diferente en empresa B.
4. Verificar que las migraciones correspondientes estén aplicadas a la base del laboratorio; hacer copia de seguridad.
5. Habilitar `Seguridad:ActivarTenancyValidada` **solo** en la configuración del laboratorio.

## Casos de prueba
1. Sin sesión, GET `/mis-empresas` y GET `/e/empresa-a/Productos`: redirigir a Login.
2. Con cuenta sin membresía, intentar GET `/e/empresa-a/Productos`: denegar.
3. Con Propietario de A, comprobar permisos permitidos; GET `/e/empresa-b/Productos` sin membresía: denegar.
4. Con Administrador de A, GET `/e/empresa-a/Reportes`: permitir. GET `/e/empresa-a/Equipo`: denegar.
5. Con Cajero de A, GET `/e/empresa-a/Facturas/Create`: permitir. GET `/e/empresa-a/Productos/Create`, GET `/e/empresa-a/Reportes` y POST `/e/empresa-a/Facturas/Anular/1`: denegar **sin mutación**.
6. Cambiar o suspender una membresía en SQL mediante flujo administrativo autorizado y comprobar que el próximo request refleje la revocación.
7. Ruta de empresa desconocida, slug malformado o método HTTP no mapeado: denegar.
8. Verificar que las consultas comerciales nunca devuelvan registros de otra empresa ni modifican inventario en una denegación.
9. Verificar respuestas de acceso denegado (403 o página AccessDenied), redirecciones de anónimos, ausencia de caching y auditoría pertinente.

## Criterio de salida
Registrar para cada caso la URL, rol, resultado HTTP real, cambio de datos (sí/no), evidencia y fecha. **No afirmar que Fase 8.5 pasó** hasta ejecutar los casos. No activar SaaS para usuarios reales antes de finalizar las pruebas.

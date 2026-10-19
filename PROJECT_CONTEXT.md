# NEXORA — Contexto compartido del proyecto

> **Nombre del producto:** Nexora — Sistema de Gestión Comercial y Facturación  
> **Repositorio:** https://github.com/Omargenao25/SistemaFacturacion  
> **Proyecto .NET / namespace actual:** `SistemaFacturacion` (no renombrar sin plan de migración).  
> **Documento actualizado:** 17 de octubre de 2026.  
> **Propósito:** fuente de contexto para Omar, Russell y sus respectivos asistentes de IA. **Verificar siempre el código y las ramas vigentes** antes de iniciar una tarea: este documento es un resumen, no reemplaza al repositorio.

## 1. Equipo y responsabilidades

| Integrante | Matrícula | GitHub | Responsabilidades acordadas |
|---|---|---|---|
| Omar Genao | MT-2023-00455 | [Omargenao25](https://github.com/Omargenao25) | Coordinación, facturación, inventario, reportes, experiencia visual e integración |
| Russell Inoa | MT-2023-00456 | [Russell-source](https://github.com/Russell-source) | Autenticación, usuarios, roles y permisos; revisión de seguridad |

- **Equipo de 2 personas:** pruebas, documentación, revisión de pull requests e integración son responsabilidades compartidas.
- Russell tiene permiso `write` sobre el repositorio, comprobado el 17/10/2026.
- Antes de modificar archivos compartidos (`Program.cs`, `Data/ApplicationDbContext.cs`, `Views/Shared/_Layout.cshtml`, migraciones), avisar al otro integrante.

## 2. Meta y límites del proyecto

**Objetivo:** desarrollar una aplicación web de facturación para una pequeña o mediana empresa comercial, con registro de clientes y productos, control de stock, venta de múltiples artículos, impuestos, descuentos, formas de pago, historial y reportes administrativos.

**Alcance académico:** comprobantes internos de venta. **No es** un sistema de emisión fiscal validado por DGII/e-CF, no procesa pagos bancarios reales y no representa certificación tributaria. La tasa de ITBIS 18% está fijada actualmente en el código como simplificación académica; revisar tratamiento de precios, descuentos, exenciones y normativa si se desea un uso real.

## 3. Tecnologías e instalación

- ASP.NET Core MVC sobre **.NET 8**, lenguaje C#.
- **Entity Framework Core**, proveedor **Microsoft SQL Server**.
- **ASP.NET Core Identity** para autenticación.
- Razor Views, HTML, CSS, JavaScript, Bootstrap, Bootstrap Icons.
- Visual Studio, Git y GitHub; base de datos local o SQL Server según `DefaultConnection`.

**Rutas importantes:**

```text
SistemaFacturacion.sln
SistemaFacturacion.csproj
Program.cs
Data/ApplicationDbContext.cs
Data/Migrations/
Models/
ViewModels/
Controllers/
Views/
  Shared/_Layout.cshtml
  Home/Index.cshtml
  Clientes/
  Categorias/
  Productos/
  Facturas/
  Reportes/
wwwroot/css/site.css
```

**Primer arranque en Windows/Visual Studio:**

1. Clonar el repositorio, abrir `SistemaFacturacion.sln`.
2. Tener el SDK .NET 8 y una instancia de SQL Server/LocalDB disponible.
3. Configurar `ConnectionStrings:DefaultConnection` mediante configuración local o Secret Manager **sin subir contraseñas ni credenciales a GitHub**.
4. Restaurar paquetes y compilar (`dotnet restore`, `dotnet build`).
5. Aplicar migraciones **solo a la base de datos correcta** (`Update-Database` desde Package Manager Console o `dotnet ef database update` con la herramienta instalada).
6. Ejecutar y verificar las rutas `/`, `/Clientes`, `/Categorias`, `/Productos`, `/Facturas`, `/Reportes`.

**Importante:** cada desarrollador debe usar su propia base de datos de desarrollo y mantener configuraciones privadas fuera del control de versiones. No confiar en que los datos locales de uno estén sincronizados con los del otro.

## 4. Estado observado en GitHub (17/10/2026)

Estas funcionalidades **tienen código en el repositorio**; no se afirma que hayan pasado una batería automatizada o pruebas concurrentes.

| Módulo | Código presente | Observación |
|---|---|---|
| Clientes | Modelo, controlador y vistas CRUD | Existe estructura generada con EF/MVC |
| Categorías | Modelo, controlador y vistas CRUD | Se relaciona con Productos |
| Productos | Modelo, controlador y vistas CRUD | Incluye precio, costo, stock, estado y categoría |
| Facturación | `FacturasController`, modelos, vista de creación | Múltiples productos, cálculo de subtotal, ITBIS, descuento y total |
| Inventario | Descuento de stock al facturar y restitución al anular | Requiere pruebas de concurrencia y casos límite |
| Pagos | Modelo `Pago` y registro en factura | Medio de pago informativo; no integración con pasarela |
| Historial | Filtros por número/cliente, estado y fechas | Vista y consulta implementadas |
| Comprobante | Vista `Views/Facturas/Details.cshtml` | Incluye opción de impresión del navegador |
| Reportes | `ReportesController`, vista, ViewModel | Ventas por fechas, más vendidos, bajo stock y ganancia bruta estimada |
| Dashboard | `HomeController`, vista de inicio | Conteos, ventas del día, últimas facturas y alertas de stock |
| Autenticación | ASP.NET Identity inicial | **Roles y permisos operativos todavía pendientes** |
| Nueva identidad visual | Rama `feature/nexora-ui` | **No está incorporada a `master`** |

**Detalles importantes confirmados del código de `master`:**

- `Program.cs` usa `AddDefaultIdentity<IdentityUser>` con `RequireConfirmedAccount = true`. **Todavía no registra roles con `AddRoles<IdentityRole>()`**; Russell debe evaluar el flujo de confirmación de correo en el entorno de desarrollo.
- `ApplicationDbContext` hereda de `IdentityDbContext` y define `DbSet` para `Cliente`, `Categoria`, `Producto`, `Factura`, `DetalleFactura` y `Pago`.
- `FacturasController` no muestra actualmente atributos de autorización por roles. **La existencia de un login no equivale a proteger las operaciones.**
- `DetalleFactura` conserva `Precio` y `CostoUnitario` de la venta; es útil para reportes históricos.
- Facturación calcula ITBIS con `subtotal * 0.18m`, y usa transacción al guardar factura, detalles, pago y descuento de stock.
- Anulación cambia el estado a `Anulada` y devuelve existencias. Confirmar permisos adecuados y resistencia a solicitudes simultáneas antes de darlo por cerrado.
- Reportes contabiliza únicamente facturas con estado `Emitida`.
- Todavía existe la marca **FacturaPro** en el layout de `master`; el rediseño de la otra rama introduce **Nexora**.
- La estimación de ganancia es académica: no resta necesariamente todos los gastos operativos y las ventas previas a `CostoUnitario` pueden tener costo histórico cero.

## 5. Ramas y método de colaboración

**Estado verificado 17/10/2026:**

- `master`: rama principal del repositorio.
- `feature/nexora-ui`: creada por Omar/asistente; contiene rediseño visual en revisión.
- **PR [#1 — Rediseño Nexora](https://github.com/Omargenao25/SistemaFacturacion/pull/1):** abierto en borrador contra `master`, **sin fusionar**.
- `feature/usuarios-roles`: **propuesta para Russell; no constaba creada** en la consulta del 17/10/2026.

### Russell: iniciar rama desde master

```bash
git fetch origin
git switch master
git pull origin master
git switch -c feature/usuarios-roles
git push -u origin feature/usuarios-roles
```

Si la rama ya existe, **no volver a crearla**: usar `git switch feature/usuarios-roles` y sincronizarla. Comprobar siempre `git status` antes de cambiar de rama; primero guardar o confirmar cambios locales.

### Omar: trabajar en el rediseño

```bash
git fetch origin
git switch feature/nexora-ui
git pull origin feature/nexora-ui
```

**Reglas de integración:**

1. Cada integrante desarrolla en su rama y registra commits descriptivos.
2. Abrir PR contra `master` cuando haya una unidad revisable; usar borrador si falta probar.
3. Revisar diffs y conflictos; no sobrescribir código del otro integrante.
4. Validar compilación, migraciones y pruebas manuales **antes** del merge.
5. Si un cambio afecta la estructura SQL, coordinar una migración por cambio y probarla sobre base de datos de desarrollo con copia de seguridad.
6. Evitar subir `bin/`, `obj/`, datos personales reales, claves, certificados y archivos con secretos.
7. Las ramas tienen historia compartida, pero **las conversaciones individuales de ChatGPT no se sincronizan**. Actualizar este archivo y los PR cuando cambie una decisión importante.

## 6. Trabajo de Russell: usuarios, roles y permisos

**Objetivo del módulo:** proteger las rutas y operaciones con ASP.NET Core Identity y roles **Administrador** y **Cajero**, compatibles con el proyecto existente.

**Matriz propuesta de autorización (confirmar con Omar antes de implementar):**

| Operación | Administrador | Cajero |
|---|---|---|
| Iniciar sesión / cerrar sesión | Sí | Sí |
| Dashboard, lectura de clientes y productos | Sí | Sí |
| Crear y editar clientes | Sí | Sí |
| Crear facturas | Sí | Sí |
| Consultar e imprimir facturas | Sí | Sí |
| Anular facturas | Sí | No |
| Crear, editar o eliminar productos/categorías | Sí | No |
| Modificar existencias directamente | Sí | No |
| Consultar reportes financieros / de ganancias | Sí | No |
| Gestionar cuentas y asignar roles | Sí | No |

**Orden de trabajo sugerido:**

1. Inspeccionar `Program.cs`, `ApplicationDbContext` y el área `Areas/Identity`.
2. Configurar Identity con roles (por ejemplo `AddRoles<IdentityRole>()`) y verificar la configuración de autenticación/autorización.
3. Diseñar inicialización de roles y creación del primer administrador **sin credenciales fijas dentro del repositorio**.
4. Definir cómo se invitan/crean usuarios. Evitar que el registro público otorgue privilegios de Administrador.
5. Aplicar `[Authorize]` y `[Authorize(Roles = "...")]` a rutas/acciones apropiadas, con una política de denegación clara.
6. Mantener el menú coherente con permisos, **sin confiar en esconder botones como medida de seguridad**.
7. Añadir pruebas: invitado no puede facturar, cajero no anula ni gestiona productos, administrador accede, usuario no autorizado recibe respuesta correcta.
8. Revisar `RequireConfirmedAccount = true` y el mecanismo de correo de confirmación para las pruebas locales.
9. Subir PR de `feature/usuarios-roles` a `master` e incluir capturas o evidencia de pruebas.

**No modificar innecesariamente** `Views/Shared/_Layout.cshtml` mientras avanza el PR de Nexora. Si Russell necesita añadir opciones de usuario, coordinar en el PR para resolver el punto de integración.

## 7. Trabajo de Omar: interfaz y ventas

**Rama `feature/nexora-ui` · PR [#1](https://github.com/Omargenao25/SistemaFacturacion/pull/1) en borrador y todavía sin fusionar (actualización 17/10/2026).**

**Cambios visuales subidos a la rama:** identidad Nexora, navegación móvil y activa, Dashboard, listados de Clientes/Categorías/Productos, formulario Nueva Factura, Detalle/Comprobante e impresión A4, Historial con filtros, Reportes con métricas e inventario.

**Evidencia disponible:** Omar mostró capturas del Dashboard, Nueva Factura, una factura nueva emitida y la vista previa de impresión. Estos flujos se ven correctamente en esas capturas, pero **los cambios posteriores de Historial y Reportes aún no han sido compilados ni probados localmente**. La captura de impresión no permite saber si todo el pie del comprobante queda en una sola página.

**Pendientes inmediatos:** actualizar la rama local y compilar; probar filtros de Historial (estado, fecha, búsqueda), períodos y totales de Reportes, versión móvil y número de páginas al imprimir. No fusionar el PR sin validar.

**Actualización 18/10/2026:** se rediseñaron y subieron los seis formularios `Create`/`Edit` de Clientes, Categorías y Productos. También se reemplazó el ID manual de categoría de Productos por un selector con categorías existentes; `ProductosController` incluye ahora carga del selector y validación de categoría válida, **pendiente de compilar/probar**. Este controlador podría coincidir con cambios de autorización de Russell, por lo que debe revisarse al integrar.

**Próximas mejoras propuestas:**

- Revisar la compilación y crear/editar registros en las tres secciones; validar el selector de categoría y los mensajes de error.
- **Actualización 18/10/2026:** se rediseñaron las seis vistas `Details`/`Delete` de Clientes, Categorías y Productos en `feature/nexora-ui`. Los productos muestran nombre de categoría (consulta con `Include` en GET de detalle y eliminación). Se conservaron las rutas y operaciones POST de eliminación; las pantallas y la compilación **siguen pendientes de comprobar en Visual Studio**.
- Revisar el comportamiento al eliminar registros relacionados con facturas/productos: la interfaz advierte de estos riesgos, pero **todavía no implementa controles de integridad adicionales**. Para datos comerciales reales, priorizar restricciones y bajas lógicas.
- Ajustar las fichas de detalle y las pantallas de eliminación tras pruebas locales.
- Ajustar visualmente Historial/Reportes tras las pruebas de Omar; resolver errores sin tocar módulos de Russell.
- Mantener la etiqueta `comprobante interno no fiscal` y la nota de que el margen bruto del reporte es estimado **antes de descuentos y gastos**.
- Revisar reglas de negocio: cliente y productos válidos, precio con/sin impuestos, descuentos, precisión monetaria y ventas simultáneas sobre stock.

## 8. Relaciones del modelo de datos

```text
Categoria 1 ───── N Producto
Cliente   1 ───── N Factura
Factura   1 ───── N DetalleFactura
Producto  1 ───── N DetalleFactura
Factura   1 ───── N Pago (modelo permite varios pagos)
Identity (AspNetUsers, etc.) gestiona las cuentas de acceso.
```

Campos principales:

- `Cliente`: IdCliente, Nombre, Apellido, Teléfono, Correo, Dirección, Identificación Fiscal.
- `Categoria`: IdCategoria, Nombre, Descripción.
- `Producto`: IdProducto, Código, Nombre, Descripción, Precio, Costo, Stock, Estado, IdCategoria.
- `Factura`: IdFactura, NumeroFactura, IdCliente, Fecha, Subtotal, Impuesto, Descuento, Total, Estado.
- `DetalleFactura`: IdDetalle, IdFactura, IdProducto, Cantidad, Precio, CostoUnitario, Subtotal.
- `Pago`: IdPago, IdFactura, MetodoPago, Monto, Fecha.

**No interpretar estas relaciones como un diagrama definitivo de todas las restricciones SQL**: comprobar el modelo EF y migraciones vigentes para claves, cardinalidades y eliminaciones en cascada.

## 9. Casos de prueba mínimos antes de la entrega

- Registrar y editar Cliente, Categoría y Producto sin errores; relación de categoría válida.
- Una factura con dos artículos calcula subtotales, ITBIS, descuento y total según la regla aprobada.
- Una venta disminuye stock; no se permite vender más de lo disponible.
- Anular factura restituye stock una sola vez y la excluye de reportes de ventas.
- Historial busca por número/cliente y filtra estado/fechas.
- Reportes de más vendidos ignoran facturas anuladas.
- Usuarios sin sesión no pueden realizar operaciones protegidas.
- Cajero recibe denegación al intentar operaciones exclusivas de Administrador, aun accediendo directamente por URL o POST.
- Probar presentación desktop/móvil e impresión sin menú lateral.
- Probar simultaneidad de dos ventas sobre el mismo stock: **aún no verificada**.
- Hacer `dotnet build` y ejecutar la app localmente antes del merge; no dar por aprobada una prueba solo porque exista código.

## 10. Instrucciones para asistentes de IA (ambos integrantes)

Al comenzar una conversación nueva:

1. Abrir este archivo de GitHub y leerlo completo.
2. Revisar **la rama correspondiente** y **los archivos realmente actuales** antes de sugerir modificaciones; `master` puede no reflejar cambios pendientes en PR.
3. Preguntar el estado de la implementación cuando no haya evidencia de compilación o funcionamiento.
4. Trabajar en incrementos pequeños, explicando qué archivos cambian y por qué.
5. No dar por implementada una característica solo por estar en el plan.
6. No renombrar namespaces/solución/base de datos para mostrar la marca Nexora; el nombre comercial puede cambiar solo en interfaz.
7. Nunca publicar claves o cadenas de conexión con contraseñas.
8. Priorizar seguridad, consistencia del inventario e integración sin conflictos.
9. Después de decisiones importantes, proponer actualizar este mismo `PROJECT_CONTEXT.md`.

### Mensaje de inicio recomendado para Russell

> Estoy desarrollando el módulo de usuarios, roles y permisos de **Nexora** como Russell Inoa (`Russell-source`). Trabajamos con Omar Genao en `Omargenao25/SistemaFacturacion`. Lee primero `PROJECT_CONTEXT.md` de `master` y el estado actual del código; después ayúdame a implementar Identity con Administrador/Cajero en `feature/usuarios-roles` sin tocar el rediseño pendiente del PR #1. Vamos archivo por archivo, explicando el código y preparando pruebas y un PR. No supongas que mi rama existe hasta comprobarlo.

### Mensaje de inicio recomendado para Omar

> Continúa conmigo el rediseño de Nexora en `feature/nexora-ui`. Lee `PROJECT_CONTEXT.md` desde `master`, revisa PR #1 y el estado real de la rama antes de modificar código. Mantén separado el módulo de roles de Russell y protege las funcionalidades de facturación e inventario.

---

**Mantenimiento:** actualizar las secciones de estado, ramas y pruebas al fusionar PRs o cambiar el alcance. Este documento no contiene credenciales, datos de clientes reales ni sustitutos de pruebas técnicas.

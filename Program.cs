using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using SistemaFacturacion.Security;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacion.Data;
using SistemaFacturacion.Services.Tenancy;

var builder = WebApplication.CreateBuilder(args);

// Fase de integración: el modo validado debe activarse EXPLÍCITAMENTE.
// Sin flag, seguimos usando el contexto mononegocio solo en Development;
// fuera de Development ese contexto devuelve Guid.Empty (fail closed).
var activarTenancyValidada = builder.Configuration.GetValue<bool>(
    "Seguridad:ActivarTenancyValidada");

builder.Services.AddScoped<ContextoEmpresaVerificado>();
builder.Services.AddScoped<GestionMembresiasSegura>();
builder.Services.AddScoped<IValidadorAccesoEmpresa, ValidadorAccesoEmpresaSql>();

if (activarTenancyValidada)
{
    builder.Services.AddScoped<IContextoEmpresa>(
        sp => sp.GetRequiredService<ContextoEmpresaVerificado>());
}
else
{
    builder.Services.AddScoped<IContextoEmpresa, ContextoEmpresaDesarrollo>();
}

// Autorización por EMPRESA y operación. Esta fase solo habilita un endpoint
// de diagnóstico, sin modificar las acciones comerciales existentes.
builder.Services.AddScoped<IAuthorizationHandler, PermisoEmpresaHandler>();
builder.Services.AddAuthorization(options =>
{
    foreach (var nombrePermiso in PermisosNexora.Todos)
    {
        options.AddPolicy(nombrePermiso, policy =>
            policy.RequireAuthenticatedUser()
                  .AddRequirements(new PermisoEmpresaRequirement(nombrePermiso)));
    }
});
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
// Registrar explícitamente las Razor Pages de ASP.NET Core Identity.
builder.Services.AddRazorPages(options =>
{
    // Identity login/registro no requieren sesión ni contexto de empresa.
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Login");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Register");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ForgotPassword");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/AccessDenied");
});

// En desarrollo local no hay proveedor de correo configurado para
// completar confirmaciones. Solo el entorno Development permite probar
// cuentas aún no confirmadas; en cualquier otro entorno se exige confirmación.
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    options.SignIn.RequireConfirmedAccount = !builder.Environment.IsDevelopment())
    .AddEntityFrameworkStores<ApplicationDbContext>();
// En el laboratorio SaaS se protegen todas las acciones MVC por permiso.
 // Con el flag apagado no se agregan filtros: desarrollo conserva el flujo
 // mononegocio ya probado.
builder.Services.AddScoped<FiltroPermisosComerciales>();
builder.Services.AddControllersWithViews(opciones =>
{
    if (activarTenancyValidada)
        opciones.Filters.AddService<FiltroPermisosComerciales>();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Impide almacenar páginas dinámicas y datos comerciales en cachés del navegador.
// Los archivos estáticos ya se sirvieron antes de este middleware.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        return Task.CompletedTask;
    });

    await next();
});

app.UseRouting();

// Identity debe determinar primero la identidad del usuario. Únicamente
// después se comprueba en SQL su membresía para el slug de la ruta.
app.UseAuthentication();

if (activarTenancyValidada)
{
    // Landing seguro en modo SaaS: el usuario debe elegir una empresa
    // antes de ejecutar consultas comerciales. No se asigna tenant por
    // cookie ni se selecciona automáticamente la primera membresía.
    app.Use(async (context, next) =>
    {
        if (HttpMethods.IsGet(context.Request.Method) &&
            context.Request.Path == "/")
        {
            context.Response.Redirect("/mis-empresas");
            return;
        }

        await next();
    });

    app.UseMiddleware<VerificacionMembresiaMiddleware>();
}

app.UseAuthorization();

// El contexto validado SOLO se puede crear si hay slug en ruta,
// identidad autenticada y membresía aprobada por SQL Server.
// Este enlace no está activo en el modo de desarrollo heredado.
if (activarTenancyValidada)
{
    app.MapControllerRoute(
        name: "empresa",
        pattern: "e/{empresaSlug}/{controller=Home}/{action=Index}/{id?}");
}

// En producción no se permite entrar anónimamente a controladores MVC.
// En Development se mantiene la compatibilidad para probar la fase inicial.
// La ruta de diagnóstico protegida por policy se registra mediante atributo.
var rutaMvc = app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// En modo SaaS cada acción MVC comercial pasa por FiltroPermisosComerciales.
// No colocar un challenge global sobre la ruta convencional: cuando una URL
// de Identity no coincide con su Razor Page, podría generar un bucle de Login.
// Los controladores comerciales también tienen [Authorize] explícito.
if (!app.Environment.IsDevelopment() && !activarTenancyValidada)
    rutaMvc.RequireAuthorization();

// Las rutas con atributos (como el diagnóstico empresarial) también quedan
// registradas explícitamente y exigen autenticación fuera de Development.
var rutasAtributo = app.MapControllers();
if (!app.Environment.IsDevelopment() && !activarTenancyValidada)
    rutasAtributo.RequireAuthorization();

// Diagnóstico exclusivamente del perfil aislado: confirma qué endpoint
// resuelve ASP.NET Core para Login, sin publicar tokens ni datos de usuario.
if (app.Environment.IsEnvironment("NexoraLab"))
{
    app.MapGet("/__nexora-lab/identity-route", (IEnumerable<EndpointDataSource> sources) =>
    {
        var routes = sources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Contains(
                "Identity/Account/Login", StringComparison.OrdinalIgnoreCase) == true)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Distinct()
            .ToArray();

        return Results.Json(new
        {
            entorno = "NexoraLab",
            paginaLoginRegistrada = routes.Length > 0,
            rutasLogin = routes
        });
    }).AllowAnonymous();
}

app.MapRazorPages();

app.Run();

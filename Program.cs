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

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

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

app.UseRouting();

// Identity debe determinar primero la identidad del usuario. Únicamente
// después se comprueba en SQL su membresía para el slug de la ruta.
app.UseAuthentication();
if (activarTenancyValidada)
    app.UseMiddleware<VerificacionMembresiaMiddleware>();
app.UseAuthorization();

// En producción no se permite entrar anónimamente a controladores MVC.
// En Development se mantiene la compatibilidad para probar la fase inicial.
// La ruta de diagnóstico protegida por policy se registra mediante atributo.
var rutaMvc = app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

if (!app.Environment.IsDevelopment())
    rutaMvc.RequireAuthorization();

// Las rutas con atributos (como el diagnóstico empresarial) también quedan
// registradas explícitamente y exigen autenticación fuera de Development.
var rutasAtributo = app.MapControllers();
if (!app.Environment.IsDevelopment())
    rutasAtributo.RequireAuthorization();

app.MapRazorPages();

app.Run();

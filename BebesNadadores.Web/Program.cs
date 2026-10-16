using BebesNadadores.Infrastructure;
using BebesNadadores.Infrastructure.Identity;
using BebesNadadores.Web.Authorization;
using BebesNadadores.Web.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using MudBlazor;
using MudBlazor.Services;
using BebesNadadores.Web.BackgroundServices;
using BebesNadadores.Application.Reportes;
using BebesNadadores.Application.Abstractions;
using BebesNadadores.Web.Diagnostico;
using Serilog;

/*
 * Logs (ver ConfiguracionLogs): carpeta "logs" junto a la app.
 * El logger inicial registra los errores de arranque.
 */
ConfiguracionLogs.CrearLoggerInicial();

try
{

var builder =
    WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(
    ConfiguracionLogs.Configurar);


builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();


builder.Services.AddRazorPages();

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddMudServices(config =>
{
    config.PopoverOptions.OverflowBehavior =
        OverflowBehavior.FlipNever;
});


builder.Services.AddInfrastructure(builder.Configuration);


builder.Services.AddAuthorization(options =>
{
    PoliticasSistema.Configurar(options);
});

builder.Services.AddScoped<PermisosUsuario>();

/*
 * Usuario logueado (auditoría y logs) y manejo de errores en
 * pantalla (mensajes en español + log con código).
 */
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActual, UsuarioActual>();
builder.Services.AddScoped<ManejadorErrores>();

/*
 * Usuarios desactivados / con cambio de rol:
 * - la cookie se revalida contra el security stamp cada 1 minuto
 *   (por defecto eran 30),
 * - el circuito de Blazor abierto se revalida cada 1 minuto.
 */
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval =
        TimeSpan.FromMinutes(1);
});

builder.Services.AddScoped<
    AuthenticationStateProvider,
    RevalidacionUsuarioAuthenticationStateProvider>();

builder.Services.AddHostedService<GeneracionCuotasBackgroundService>();
builder.Services.AddHostedService<RecuperosClasesNoDictadasBackgroundService>();

var app =
    builder.Build();


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    app.UseHsts();
}


app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.UseAntiforgery();


app.MapStaticAssets();

app.MapRazorPages();

app.MapGet(
    "/reportes/descargar/excel",
    async (
        DateOnly desde,
        DateOnly hasta,
        IReporteExportService exportService,
        CancellationToken ct) =>
    {
        var archivo =
            await exportService
                .ExportarExcelAsync(
                    desde,
                    hasta,
                    ct);

        return Results.File(
            archivo.Contenido,
            archivo.ContentType,
            archivo.NombreArchivo);
    })
    .RequireAuthorization(
        PoliticasSistema.ConsultaGeneral);


app.MapGet(
    "/reportes/descargar/pdf",
    async (
        DateOnly desde,
        DateOnly hasta,
        IReporteExportService exportService,
        CancellationToken ct) =>
    {
        var archivo =
            await exportService
                .ExportarPdfAsync(
                    desde,
                    hasta,
                    ct);

        return Results.File(
            archivo.Contenido,
            archivo.ContentType,
            archivo.NombreArchivo);
    })
    .RequireAuthorization(
        PoliticasSistema.ConsultaGeneral);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


using (var scope = app.Services.CreateScope())
{
    await IdentitySeeder.SeedAsync(
        scope.ServiceProvider,
        app.Configuration);
}


app.Run();

}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(
        ex,
        "La aplicación no pudo iniciar.");
}
finally
{
    Log.CloseAndFlush();
}

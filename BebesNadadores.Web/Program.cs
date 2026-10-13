using BebesNadadores.Infrastructure;
using BebesNadadores.Infrastructure.Identity;
using BebesNadadores.Web.Authorization;
using BebesNadadores.Web.Components;
using MudBlazor;
using MudBlazor.Services;
using BebesNadadores.Web.BackgroundServices;
using BebesNadadores.Application.Reportes;

var builder =
    WebApplication.CreateBuilder(args);


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

builder.Services.AddHostedService<GeneracionCuotasBackgroundService>();

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
    .RequireAuthorization(policy =>
        policy.RequireRole(
            "Administrador",
            "Administrativa",
            "SoloLectura"));


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
    .RequireAuthorization(policy =>
        policy.RequireRole(
            "Administrador",
            "Administrativa",
            "SoloLectura"));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


using (var scope = app.Services.CreateScope())
{
    await IdentitySeeder.SeedAsync(
        scope.ServiceProvider,
        app.Configuration);
}


app.Run();
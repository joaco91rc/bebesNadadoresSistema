using BebesNadadores.Web.Services.Facturas;
using BebesNadadores.Web.Services.Automatizaciones;
using Microsoft.AspNetCore.DataProtection;
using BebesNadadores.Infrastructure;
using BebesNadadores.Infrastructure.Identity;
using BebesNadadores.Web.Authorization;
using BebesNadadores.Web.Components;
using BebesNadadores.Web.Services.Finanzas;
using BebesNadadores.Web.Services.Comprobantes;
using BebesNadadores.Web.Services.Cumpleanos;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using MudBlazor;
using MudBlazor.Services;
using BebesNadadores.Web.BackgroundServices;
using BebesNadadores.Application.Reportes;
using BebesNadadores.Application.Abstractions;
using BebesNadadores.Web.Diagnostico;
using Serilog;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

// Logs (ver ConfiguracionLogs): carpeta "logs" junto a la app.
// El logger inicial registra los errores de arranque.
ConfiguracionLogs.CrearLoggerInicial();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog(ConfiguracionLogs.Configurar);

    builder.Services
        .AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddRazorPages();

    // IMPORTANTE: persistir estas claves en volumen Docker para que los enlaces
    // de cumpleaños no se invaliden al reconstruir/reiniciar el contenedor.
    var carpetaClaves = builder.Configuration["Cumpleanos:CarpetaClaves"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "ProtectionKeys");
    Directory.CreateDirectory(carpetaClaves);
    builder.Services.AddDataProtection()
        .SetApplicationName("BebesNadadores")
        .PersistKeysToFileSystem(new DirectoryInfo(carpetaClaves));

    builder.Services.AddCascadingAuthenticationState();

    builder.Services.AddMudServices(config =>
    {
        config.PopoverOptions.OverflowBehavior = OverflowBehavior.FlipNever;
    });

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddScoped<IIndicadorFinancieroService, IndicadorFinancieroService>();
    builder.Services.AddScoped<ComprobantePagoService>();
    builder.Services.AddScoped<EnlaceComprobantePagoService>();
    builder.Services.AddScoped<TarjetaCumpleanosService>();
    builder.Services.AddScoped<ComunicacionesService>();
    builder.Services.AddScoped<FacturasService>();
    builder.Services.AddScoped<BebesNadadores.Web.Services.Recuperos.RegularizacionHistoricaService>();
    builder.Services.AddScoped<BebesNadadores.Web.Services.Migracion.MigracionCargaService>();
    builder.Services.AddSingleton<BebesNadadores.Web.Services.Migracion.MigracionEjecucionService>();

    builder.Services.AddAuthorization(options =>
    {
        PoliticasSistema.Configurar(options);
    });

    builder.Services.AddScoped<PermisosUsuario>();

    // Usuario logueado (auditoría y logs) y manejo de errores en pantalla.
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IUsuarioActual, UsuarioActual>();
    builder.Services.AddScoped<ManejadorErrores>();

    // Usuarios desactivados / cambio de rol: revalidación cada 1 minuto.
    builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    {
        options.ValidationInterval = TimeSpan.FromMinutes(1);
    });

    builder.Services.AddScoped<
        AuthenticationStateProvider,
        RevalidacionUsuarioAuthenticationStateProvider>();

    // En producción, ambas tareas están habilitadas por defecto.
    // Durante la migración se pueden desactivar mediante configuración
    // sin modificar el código ni afectar otros entornos.
    bool generacionCuotasHabilitada = builder.Configuration.GetValue(
        "BackgroundServices:GeneracionCuotasHabilitada", true);

    bool recuperosAutomaticosHabilitados = builder.Configuration.GetValue(
        "BackgroundServices:RecuperosAutomaticosHabilitados", true);

    if (generacionCuotasHabilitada)
    {
        builder.Services.AddHostedService<GeneracionCuotasBackgroundService>();
    }

    if (recuperosAutomaticosHabilitados)
    {
        builder.Services.AddHostedService<RecuperosClasesNoDictadasBackgroundService>();
    }

    var app = builder.Build();

    app.Logger.LogInformation(
        "BackgroundServices: Generación de cuotas={Cuotas}; Recuperos automáticos={Recuperos}",
        generacionCuotasHabilitada ? "HABILITADA" : "DESHABILITADA",
        recuperosAutomaticosHabilitados ? "HABILITADOS" : "DESHABILITADOS");

    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto
    });

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    app.MapComunicaciones();
    app.MapFacturas();
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
            var archivo = await exportService.ExportarExcelAsync(
                desde,
                hasta,
                ct);

            return Results.File(
                archivo.Contenido,
                archivo.ContentType,
                archivo.NombreArchivo);
        })
        .RequireAuthorization(PoliticasSistema.ConsultaGeneral);

    app.MapGet(
        "/reportes/descargar/pdf",
        async (
            DateOnly desde,
            DateOnly hasta,
            IReporteExportService exportService,
            CancellationToken ct) =>
        {
            var archivo = await exportService.ExportarPdfAsync(
                desde,
                hasta,
                ct);

            return Results.File(
                archivo.Contenido,
                archivo.ContentType,
                archivo.NombreArchivo);
        })
        .RequireAuthorization(PoliticasSistema.ConsultaGeneral);

    app.MapGet("/reportes/cumpleanos/excel", async (int anio, int mes,
        BebesNadadores.Application.Bebes.IBebeService bebes, HttpContext http, CancellationToken ct) =>
    {
        if (mes is < 1 or > 12 || anio is < 1900 or > 9999) return Results.BadRequest();
        http.Response.Headers.CacheControl = "private, no-store";
        var datos = await bebes.ObtenerCumpleaniosMesAsync(anio, mes, ct);
        using var libro = new ClosedXML.Excel.XLWorkbook();
        var hoja = libro.Worksheets.Add("Cumpleaños");
        hoja.Cell(1, 1).Value = "Fecha de cumpleaños";
        hoja.Cell(1, 2).Value = "Nombre y apellido";
        hoja.Range("A1:B1").Style.Font.Bold = true;
        var fila = 2;
        foreach (var c in datos.OrderBy(x => x.FechaNacimiento.Day).ThenBy(x => x.NombreCompleto))
        {
            hoja.Cell(fila, 1).Value = new DateTime(anio, mes, Math.Min(c.FechaNacimiento.Day, DateTime.DaysInMonth(anio, mes)));
            hoja.Cell(fila, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            hoja.Cell(fila++, 2).Value = c.NombreCompleto;
        }
        hoja.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        libro.SaveAs(stream);
        return Results.File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Cumpleanos {mes:00}-{anio}.xlsx");
    }).RequireAuthorization(PoliticasSistema.GestionAdministrativa);

    app.MapGet("/pagos/comprobantes/mes", async (ComprobantePagoService comprobantes, HttpContext http, CancellationToken ct) =>
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var archivo = await comprobantes.GenerarMesAsync(ct);
        return Results.File(archivo.Contenido, "application/zip", archivo.NombreArchivo);
    }).RequireAuthorization(PoliticasSistema.GestionCaja);

    // Recibos individuales: requerir acceso a Caja. Impedir caché de datos personales.
    app.MapGet(
        "/pagos/comprobante/{pagoId:guid}",
        async (Guid pagoId, HttpContext http, ComprobantePagoService comprobantes, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "private, no-store, no-cache";
            var archivo = await comprobantes.GenerarAsync(pagoId, ct);
            return archivo is null
                ? Results.NotFound()
                : Results.File(archivo.Value.Contenido, "application/pdf", archivo.Value.NombreArchivo);
        })
        .RequireAuthorization(PoliticasSistema.GestionCaja);

    // Comprobantes públicos: solo con token cifrado, firmado y con vencimiento.
    // No se expone el GUID del pago ni se requiere iniciar sesión para descargar.
    app.MapGet(
        "/comprobantes/descargar/{token}",
        async (string token, string? t, HttpContext http, EnlaceComprobantePagoService enlaces,
            ComprobantePagoService comprobantes, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "private, no-store, no-cache, max-age=0";
            http.Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
            http.Response.Headers["Referrer-Policy"] = "no-referrer";
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";

            if (!enlaces.IntentarValidar(t ?? token, out var pagoId))
                return Results.NotFound();

            var archivo = await comprobantes.GenerarAsync(pagoId, ct);
            return archivo is null
                ? Results.NotFound()
                : Results.File(archivo.Value.Contenido, "application/pdf", archivo.Value.NombreArchivo);
        });

    // Tarjeta individual: pública solo con token firmado que caduca a los siete días.
    app.MapGet("/cumple/{token}", (string token, string? t, HttpContext http,
            TarjetaCumpleanosService tarjetas, CancellationToken ct) =>
        tarjetas.MostrarAsync(t ?? token, http, ct));

    app.MapGet("/e/{slug}", BebesNadadores.Web.Services.EnlacesCortos.ResolverAsync).AllowAnonymous();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    using (var scope = app.Services.CreateScope())
    {
        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();

        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();

        await IdentitySeeder.SeedAsync(
            scope.ServiceProvider,
            app.Configuration);
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "La aplicación no pudo iniciar.");
}
finally
{
    Log.CloseAndFlush();
}

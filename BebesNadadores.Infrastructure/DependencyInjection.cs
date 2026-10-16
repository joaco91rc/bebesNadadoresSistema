using BebesNadadores.Application.Avisos;
using BebesNadadores.Application.Busqueda;
using BebesNadadores.Infrastructure.Services.Avisos;
using BebesNadadores.Infrastructure.Services.Busqueda;
using Microsoft.EntityFrameworkCore.Diagnostics;
using BebesNadadores.Application.Abstractions;
using BebesNadadores.Infrastructure.Persistence.Auditoria;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BebesNadadores.Application.Anulaciones;
using BebesNadadores.Application.AptosMedicos;
using BebesNadadores.Application.Asistencia;
using BebesNadadores.Application.Bancos;
using BebesNadadores.Application.Bebes;
using BebesNadadores.Application.Caja;
using BebesNadadores.Application.CiclosLectivos;
using BebesNadadores.Application.Comunicaciones;
using BebesNadadores.Application.Configuracion;
using BebesNadadores.Application.ControlDocente;
using BebesNadadores.Application.Cuotas;
using BebesNadadores.Application.Dashboard;
using BebesNadadores.Application.Inscripciones;
using BebesNadadores.Application.ListaEspera;
using BebesNadadores.Application.Niveles;
using BebesNadadores.Application.Pagos;
using BebesNadadores.Application.Profesores;
using BebesNadadores.Application.Proveedores;
using BebesNadadores.Application.Recuperos;
using BebesNadadores.Application.Reportes;
using BebesNadadores.Application.Turnera;
using BebesNadadores.Infrastructure.Identity;
using BebesNadadores.Infrastructure.Persistence;
using BebesNadadores.Infrastructure.Services.Anulaciones;
using BebesNadadores.Infrastructure.Services.AptosMedicos;
using BebesNadadores.Infrastructure.Services.Asistencias;
using BebesNadadores.Infrastructure.Services.Bancos;
using BebesNadadores.Infrastructure.Services.Bebes;
using BebesNadadores.Infrastructure.Services.Caja;
using BebesNadadores.Infrastructure.Services.CiclosLectivos;
using BebesNadadores.Infrastructure.Services.Comunicaciones;
using BebesNadadores.Infrastructure.Services.Configuracion;
using BebesNadadores.Infrastructure.Services.ControlDocente;
using BebesNadadores.Infrastructure.Services.Cuotas;
using BebesNadadores.Infrastructure.Services.Dashboard;
using BebesNadadores.Infrastructure.Services.Inscripciones;
using BebesNadadores.Infrastructure.Services.ListaEspera;
using BebesNadadores.Infrastructure.Services.Niveles;
using BebesNadadores.Infrastructure.Services.Pagos;
using BebesNadadores.Infrastructure.Services.Profesores;
using BebesNadadores.Infrastructure.Services.Proveedores;
using BebesNadadores.Infrastructure.Services.Recuperos;
using BebesNadadores.Infrastructure.Services.Reportes;
using BebesNadadores.Infrastructure.Services.Turnera;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BebesNadadores.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "No se encontró ConnectionStrings:DefaultConnection."
            );

        // =====================================================
        // DATABASE
        // =====================================================

        /*
         * Auditoría automática (CreadoPor / ModificadoPor): el
         * interceptor necesita al usuario del circuito / request, por
         * eso la fábrica es Scoped (en Blazor Server, un scope por
         * usuario conectado). La Web registra el IUsuarioActual real;
         * sin Web (jobs, pruebas) queda SinUsuarioActual.
         */
        services.TryAddScoped<IUsuarioActual, SinUsuarioActual>();
        services.AddScoped<AuditoriaInterceptor>();

        services.AddDbContextFactory<AppDbContext>(
            (sp, options) =>
            {
                options.UseNpgsql(
                    connectionString,
                    npgsql =>
                    {
                        npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    }
                );

                options.AddInterceptors(
                    sp.GetRequiredService<AuditoriaInterceptor>());

                /*
                 * Aviso informativo de EF ("varios Include de colecciones")
                 * que no es un error: sin esto llenaba errores-*.log.
                 */
                options.ConfigureWarnings(w =>
                    w.Ignore(RelationalEventId.MultipleCollectionIncludeWarning));
            },
            ServiceLifetime.Scoped);

        // =====================================================
        // AUTHENTICATION
        // =====================================================

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;

                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        // =====================================================
        // IDENTITY
        // =====================================================

        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = 5;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // =====================================================
        // SERVICES
        // =====================================================

        services.AddScoped<ITurneraService, TurneraService>();
        services.AddScoped<GeneradorRecuperosClasesNoDictadas>();
        services.AddScoped<IRecuperosClasesNoDictadasService>(
            sp => sp.GetRequiredService<GeneradorRecuperosClasesNoDictadas>());
        services.AddScoped<IRecuperosInicioMitadMesService, GeneradorRecuperosInicioMitadMes>();

        services.AddScoped<IDiaNoLaborableService, DiaNoLaborableService>();

        services.AddScoped<IAsistenciaService, AsistenciaService>();

        services.AddScoped<INivelService, NivelService>();

        services.AddScoped<IProfesorService, ProfesorService>();

        services.AddScoped<IBebeService, BebeService>();
        services.AddScoped<IBebeLegajoService, BebeLegajoService>();

        services.AddScoped<IInscripcionConsultaService, InscripcionConsultaService>();
        services.AddScoped<IInscripcionService, InscripcionService>();
        services.AddScoped<IRecuperosService, RecuperosService>();
        services.AddScoped<IListaEsperaService, ListaEsperaService>();
        services.AddScoped<ICicloLectivoService, CicloLectivoService>();
        services.AddScoped<IControlDocenteService, ControlDocenteService>();
        services.AddScoped<IBancoService, BancoService>();
        services.AddScoped<ICuentaBancariaService, CuentaBancariaService>();
        services.AddScoped<IMovimientoBancarioService, MovimientoBancarioService>();
        services.AddScoped<RegistradorCobros>();
        services.AddScoped<IAnulacionService, AnulacionService>();
        services.AddScoped<IPagoService, PagoService>();
        services.AddScoped<ICajaService, CajaService>();
        services.AddScoped<IConceptoCajaService, ConceptoCajaService>();
        services.AddScoped<IProveedorService, ProveedorService>();
        services.AddScoped<IPagoProveedorService, PagoProveedorService>();
        services.AddScoped<IGeneradorCuotasService, GeneradorCuotasService>();
        services.AddScoped<IWhatsAppService, WhatsAppService>();
        services.AddScoped<IAptoMedicoService, AptoMedicoService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IBusquedaGlobalService, BusquedaGlobalService>();
        services.AddScoped<IAvisosService, AvisosService>();
        services.AddScoped<IConfiguracionSistemaService, ConfiguracionSistemaService>();
        services.AddScoped<IReporteService, ReporteService>();
        services.AddScoped<IReporteExportService, ReporteExportService>();
        services.AddScoped<IUsuarioAdministracionService, UsuarioAdministracionService>();
        services.AddScoped<IPlanTarifaService, PlanTarifaService>();

        return services;
    }
}

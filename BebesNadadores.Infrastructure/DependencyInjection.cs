using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using BebesNadadores.Application.Asistencia;
using BebesNadadores.Application.Turnera;
using BebesNadadores.Application.Niveles;
using BebesNadadores.Application.Profesores;
using BebesNadadores.Application.Bebes;

using BebesNadadores.Application.Inscripciones;
using BebesNadadores.Application.Recuperos;
using BebesNadadores.Application.ListaEspera;
using BebesNadadores.Application.CiclosLectivos;
using BebesNadadores.Application.ControlDocente;


using BebesNadadores.Infrastructure.Identity;
using BebesNadadores.Infrastructure.Persistence;
using BebesNadadores.Infrastructure.Services.Asistencias;
using BebesNadadores.Infrastructure.Services.Turnera;
using BebesNadadores.Infrastructure.Services.Niveles;
using BebesNadadores.Infrastructure.Services.Profesores;
using BebesNadadores.Infrastructure.Services.Bebes;

using BebesNadadores.Infrastructure.Services.Inscripciones;
using BebesNadadores.Infrastructure.Services.Recuperos;
using BebesNadadores.Infrastructure.Services.ListaEspera;
using BebesNadadores.Infrastructure.Services.CiclosLectivos;
using BebesNadadores.Infrastructure.Services.ControlDocente;


namespace BebesNadadores.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "No se encontró ConnectionStrings:DefaultConnection.");


        // =====================================================
        // DATABASE
        // =====================================================

        services.AddDbContextFactory<AppDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsql =>
                {
                    npgsql.MigrationsAssembly(
                        typeof(AppDbContext)
                            .Assembly
                            .FullName);
                });
        });


        // =====================================================
        // AUTHENTICATION
        // =====================================================

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme =
                    IdentityConstants.ApplicationScheme;

                options.DefaultSignInScheme =
                    IdentityConstants.ExternalScheme;
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

                options.Lockout.DefaultLockoutTimeSpan =
                    TimeSpan.FromMinutes(15);

                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();


        // =====================================================
        // SERVICES
        // =====================================================

        services.AddScoped< ITurneraService,TurneraService>();

        services.AddScoped<IDiaNoLaborableService,DiaNoLaborableService>();

        services.AddScoped<IAsistenciaService,AsistenciaService>();

        services.AddScoped<INivelService,NivelService>();

        services.AddScoped< IProfesorService, ProfesorService>();

           services.AddScoped<IBebeService, BebeService>();
            services.AddScoped<IBebeLegajoService, BebeLegajoService>();

            services.AddScoped< IInscripcionConsultaService,  InscripcionConsultaService>();
            services.AddScoped<IInscripcionService, InscripcionService>();
            services.AddScoped<IRecuperacionService,RecuperacionService>();
            services.AddScoped<IListaEsperaService, ListaEsperaService>();
            services.AddScoped< ICicloLectivoService, CicloLectivoService>();
            services.AddScoped< IControlDocenteService, ControlDocenteService>();
        return services;
    }
}
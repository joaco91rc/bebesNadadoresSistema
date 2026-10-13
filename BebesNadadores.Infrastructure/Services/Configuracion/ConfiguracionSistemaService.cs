using BebesNadadores.Application.Configuracion;
using BebesNadadores.Application.Configuracion.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Configuracion;

public sealed class ConfiguracionSistemaService(
    IDbContextFactory<AppDbContext> factory)
    : IConfiguracionSistemaService
{
    public async Task<ConfiguracionSistemaDto> ObtenerAsync(
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        var configuracion =
            await ObtenerOCrearAsync(
                db,
                ct);

        return Mapear(configuracion);
    }


    public async Task GuardarInstitucionAsync(
        GuardarInstitucionDto dto,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(
            dto.NombreInstitucion))
        {
            throw new InvalidOperationException(
                "Debe indicar el nombre de la institución.");
        }

        await using var db =
            await factory.CreateDbContextAsync(ct);

        var configuracion =
            await ObtenerOCrearAsync(
                db,
                ct);

        configuracion.NombreInstitucion =
            dto.NombreInstitucion.Trim();

        configuracion.WhatsApp =
            Normalizar(dto.WhatsApp);

        configuracion.Direccion =
            Normalizar(dto.Direccion);

        configuracion.LogoUrl =
            Normalizar(dto.LogoUrl);

        configuracion.ModificadoUtc =
            DateTimeOffset.UtcNow;

        configuracion.ModificadoPorUsuarioId =
            usuarioId == Guid.Empty
                ? null
                : usuarioId;

        await db.SaveChangesAsync(ct);
    }


    public async Task GuardarReglasAsync(
        GuardarReglasSistemaDto dto,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        ValidarReglas(dto);

        await using var db =
            await factory.CreateDbContextAsync(ct);

        var configuracion =
            await ObtenerOCrearAsync(
                db,
                ct);

        configuracion.DiasVigenciaRecupero =
            dto.DiasVigenciaRecupero;

        configuracion.MesesVigenciaApto =
            dto.MesesVigenciaApto;

        configuracion.DiasAlertaApto =
            dto.DiasAlertaApto;

        configuracion.DiaVencimientoCuota =
            dto.DiaVencimientoCuota;

        configuracion.EdadMaximaMeses =
            dto.EdadMaximaMeses;

        configuracion.ModificadoUtc =
            DateTimeOffset.UtcNow;

        configuracion.ModificadoPorUsuarioId =
            usuarioId == Guid.Empty
                ? null
                : usuarioId;

        await db.SaveChangesAsync(ct);
    }


    private static async Task<ConfiguracionSistema>
        ObtenerOCrearAsync(
            AppDbContext db,
            CancellationToken ct)
    {
        var configuracion =
            await db.Set<ConfiguracionSistema>()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            ConfiguracionSistema
                                .RegistroUnicoId,
                    ct);

        if (configuracion is not null)
        {
            return configuracion;
        }

        configuracion =
            new ConfiguracionSistema
            {
                Id =
                    ConfiguracionSistema
                        .RegistroUnicoId,

                NombreInstitucion =
                    "Bebés Nadadores",

                DiasVigenciaRecupero = 45,
                MesesVigenciaApto = 1,
                DiasAlertaApto = 15,
                DiaVencimientoCuota = 10,
                EdadMaximaMeses = 84
            };

        db.Set<ConfiguracionSistema>()
            .Add(configuracion);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            /*
             * Dos solicitudes podrían intentar crear el registro
             * único al mismo tiempo durante el primer acceso.
             * Si ocurrió, descartamos el alta local y recuperamos
             * el registro que ganó la carrera.
             */
            db.Entry(configuracion).State =
                EntityState.Detached;

            configuracion =
                await db.Set<ConfiguracionSistema>()
                    .FirstAsync(
                        x =>
                            x.Id ==
                                ConfiguracionSistema
                                    .RegistroUnicoId,
                        ct);
        }

        return configuracion;
    }


    private static void ValidarReglas(
        GuardarReglasSistemaDto dto)
    {
        if (dto.DiasVigenciaRecupero <= 0)
        {
            throw new InvalidOperationException(
                "La vigencia de los recuperos debe ser mayor a cero.");
        }

        if (dto.MesesVigenciaApto <= 0)
        {
            throw new InvalidOperationException(
                "La vigencia del apto médico debe ser mayor a cero.");
        }

        if (dto.DiasAlertaApto < 0)
        {
            throw new InvalidOperationException(
                "Los días de alerta del apto no pueden ser negativos.");
        }

        if (dto.DiaVencimientoCuota is < 1 or > 28)
        {
            throw new InvalidOperationException(
                "El día de vencimiento de cuota debe estar entre 1 y 28.");
        }

        if (dto.EdadMaximaMeses is < 1 or > 84)
        {
            throw new InvalidOperationException(
                "La edad máxima debe estar entre 1 y 84 meses (7 años).");
        }
    }


    private static ConfiguracionSistemaDto Mapear(
        ConfiguracionSistema x)
    {
        return new ConfiguracionSistemaDto(
            x.Id,
            x.NombreInstitucion,
            x.WhatsApp,
            x.Direccion,
            x.LogoUrl,
            x.DiasVigenciaRecupero,
            x.MesesVigenciaApto,
            x.DiasAlertaApto,
            x.DiaVencimientoCuota,
            x.EdadMaximaMeses);
    }


    private static string? Normalizar(
        string? texto)
    {
        return string.IsNullOrWhiteSpace(texto)
            ? null
            : texto.Trim();
    }
}

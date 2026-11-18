using System.Security.Cryptography;
using System.Text;

namespace BebesNadadores.Web.Services.Automatizaciones;

public static class ComunicacionesEndpoints
{
    public sealed record Resultado(Guid Id, bool Enviado);
    public static void MapComunicaciones(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/automatizaciones").AllowAnonymous();
        grupo.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            http.Response.Headers.CacheControl = "no-store";
            var config = http.RequestServices.GetRequiredService<IConfiguration>();
            var secreto = config["Automatizaciones:ApiKey"];
            var recibido = http.Request.Headers["X-Automatizaciones-Key"].ToString();
            if (string.IsNullOrWhiteSpace(secreto) || secreto.Length < 32 || recibido.Length > 256 ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(secreto), Encoding.UTF8.GetBytes(recibido)))
                return Results.Unauthorized();
            return await next(context);
        });
        grupo.MapGet("/estado", (ComunicacionesService service) => Results.Ok(new { habilitada = service.HabilitadaGlobal }));
        grupo.MapPost("/{tipo}/tomar", async (string tipo, ComunicacionesService service, CancellationToken ct) =>
        {
            if (!ComunicacionesService.Tipos.Contains(tipo)) return Results.NotFound();
            var mensaje = await service.TomarAsync(tipo, ct);
            return Results.Ok(new { mensaje });
        });
        grupo.MapPost("/resultado", async (Resultado resultado, ComunicacionesService service, CancellationToken ct) =>
            Results.Ok(new { actualizado = await service.ResultadoAsync(resultado.Id, resultado.Enviado, ct) }));
    }
}

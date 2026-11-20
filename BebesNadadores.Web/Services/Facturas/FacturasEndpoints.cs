using System.Security.Cryptography;
using System.Text;
using BebesNadadores.Web.Authorization;
namespace BebesNadadores.Web.Services.Facturas;
public static class FacturasEndpoints
{
    public sealed record Resultado(Guid Id,bool Enviado);
    public static void MapFacturas(this WebApplication app)
    {
        app.MapGet("/facturas/archivo/{id:guid}",async (Guid id,FacturasService s,HttpContext h)=>{
            h.Response.Headers.CacheControl="private, no-store";
            h.Response.Headers["X-Content-Type-Options"]="nosniff";
            var bytes=await s.PdfAsync(id);
            return bytes==null?Results.NotFound():Results.File(bytes,"application/pdf",$"Factura-{id:N}.pdf");
        }).RequireAuthorization(PoliticasSistema.GestionCaja);
        var g=app.MapGroup("/api/facturas").AllowAnonymous();
        g.AddEndpointFilter(async (context,next)=>{
            var h=context.HttpContext; h.Response.Headers.CacheControl="no-store";
            var key=h.RequestServices.GetRequiredService<IConfiguration>()["Automatizaciones:ApiKey"];
            var supplied=h.Request.Headers["X-Automatizaciones-Key"].ToString();
            if(string.IsNullOrWhiteSpace(key)||key.Length<32||supplied.Length>256||!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key),Encoding.UTF8.GetBytes(supplied)))return Results.Unauthorized();
            return await next(context);
        });
        g.MapPost("/tomar",async(FacturasService s)=>Results.Ok(new {mensaje=await s.TomarAsync()}));
        g.MapPost("/resultado",async(Resultado r,FacturasService s)=>{await s.ResultadoAsync(r.Id,r.Enviado);return Results.Ok();});
    }
}

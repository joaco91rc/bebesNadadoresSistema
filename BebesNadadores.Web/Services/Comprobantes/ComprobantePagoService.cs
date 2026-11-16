using System.Globalization;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BebesNadadores.Web.Services.Comprobantes;

/// <summary>
/// Recibo de un pago ya confirmado. Nunca recibe importe, alumno ni fecha desde el navegador.
/// Los datos se consultan desde PostgreSQL para evitar comprobantes adulterables.
/// </summary>
public sealed class ComprobantePagoService(
    IDbContextFactory<AppDbContext> factory,
    IWebHostEnvironment environment)
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    // Igual que ReporteExportService: verificar el cumplimiento de la licencia Community.
    static ComprobantePagoService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<(byte[] Contenido, string NombreArchivo)?> GenerarAsync(
        Guid pagoId,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var pago = await db.Pagos.AsNoTracking()
            .Include(p => p.Bebe)
            .FirstOrDefaultAsync(p => p.Id == pagoId, ct);

        if (pago is null || pago.AnuladoUtc.HasValue)
            return null;

        var nombreAlumno = (pago.Bebe.Nombre + " " + pago.Bebe.Apellido).Trim();
        var fechaLocal = TimeZoneInfo.ConvertTime(pago.FechaUtc, ZonaArgentina());
        var concepto = pago.Observaciones?.StartsWith(
            "Reserva vacante ciclo", StringComparison.OrdinalIgnoreCase) == true
            ? "Reserva de vacante" : "Pago recibido";
        var logoRuta = Path.Combine(
            environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"),
            "images", "logoBebesTransparente.png");
        byte[]? logo = File.Exists(logoRuta) ? await File.ReadAllBytesAsync(logoRuta, ct) : null;
        var importeTexto = pago.Importe.ToString("C2", Cultura);

        using var archivo = new MemoryStream();
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(11).FontColor(Colors.Grey.Darken3));
                page.Content().Column(column =>
                {
                    column.Spacing(12);
                    column.Item().AlignCenter().Width(94).Height(94)
                        .Element(c =>
                        {
                            if (logo is not null) c.Image(logo).FitArea();
                        });
                    column.Item().AlignCenter().Text("BEBÉS NADADORES")
                        .Bold().FontSize(17).FontColor("#136F9D");
                    column.Item().AlignCenter().Text("COMPROBANTE DE PAGO")
                        .FontSize(12).FontColor(Colors.Grey.Darken2);
                    column.Item().PaddingTop(7).BorderTop(1).BorderColor("#D2EAF5")
                        .PaddingTop(14).Column(datos =>
                        {
                            datos.Spacing(12);
                            datos.Item().Text($"Recibimos de: {nombreAlumno}");
                            datos.Item().Text($"Fecha: {fechaLocal:dd/MM/yyyy HH:mm}");
                            datos.Item().Text($"Concepto: {concepto}");
                            datos.Item().Text($"Medio de pago: {pago.MedioPago}");
                            datos.Item().PaddingVertical(8).Background("#E8F6FC")
                                .Padding(12).AlignCenter().Text(importeTexto)
                                .Bold().FontSize(24).FontColor("#136F9D");
                        });
                    column.Item().PaddingTop(5).Text("Se recibió el importe indicado.")
                        .FontSize(10).FontColor(Colors.Grey.Darken2);
                    column.Item().Text($"N.º de operación: {pago.Id:N}")
                        .FontSize(8).FontColor(Colors.Grey.Medium);
                    column.Item().Text("Este comprobante acredita el pago recibido, no necesariamente la cancelación total de las cuotas.")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });
        documento.GeneratePdf(archivo);
        return (archivo.ToArray(), $"BebesNadadores_Recibo_{pago.Id:N}.pdf");
    }

    private static TimeZoneInfo ZonaArgentina()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time"); }
    }
}

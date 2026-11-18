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
            .Include(p => p.Imputaciones).ThenInclude(x => x.Cuota)
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

        // Estética institucional oscura: igual que el panel de Bebés Nadadores.
        // Las leyendas usan alto contraste para verse también al imprimir.
        using var archivo = new MemoryStream();
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(30);
                page.PageColor("#071F32");
                page.DefaultTextStyle(t => t.FontSize(11).FontColor("#F4FBFF"));
                page.Content().Column(column =>
                {
                    column.Spacing(15);

                    column.Item().AlignCenter().Width(110).Height(110)
                        .Background("#0E4666")
                        .Border(1).BorderColor("#268AB4")
                        .Padding(13)
                        .Element(c =>
                        {
                            if (logo is not null)
                                c.AlignCenter().AlignMiddle().Image(logo).FitArea();
                        });

                    column.Item().AlignCenter().Text("BEBÉS NADADORES")
                        .Bold().FontSize(19).FontColor("#FFFFFF");
                    column.Item().AlignCenter().Text("COMPROBANTE DE PAGO")
                        .Bold().FontSize(11).FontColor("#74D9F7");

                    column.Item().PaddingTop(5)
                        .BorderTop(1).BorderColor("#2B7798")
                        .PaddingTop(20).Column(datos =>
                        {
                            datos.Spacing(13);
                            datos.Item().Text(texto =>
                            {
                                texto.Span("Recibimos de: ").FontColor("#8CDDF4");
                                texto.Span(nombreAlumno).SemiBold().FontColor("#FFFFFF");
                            });
                            datos.Item().Text(texto =>
                            {
                                texto.Span("Fecha: ").FontColor("#8CDDF4");
                                texto.Span(fechaLocal.ToString("dd/MM/yyyy HH:mm", Cultura)).FontColor("#FFFFFF");
                            });
                            datos.Item().Text(texto =>
                            {
                                texto.Span("Concepto: ").FontColor("#8CDDF4");
                                texto.Span(concepto).FontColor("#FFFFFF");
                            });
                            datos.Item().Text(texto =>
                            {
                                texto.Span("Medio de pago: ").FontColor("#8CDDF4");
                                texto.Span(pago.MedioPago.ToString()).FontColor("#FFFFFF");
                            });
                        });

                    column.Item().PaddingTop(8).Background("#103C58")
                        .Border(1).BorderColor("#2C89B1")
                        .PaddingVertical(16).PaddingHorizontal(12)
                        .Column(total =>
                        {
                            total.Spacing(5);
                            total.Item().AlignCenter().Text("IMPORTE RECIBIDO")
                                .FontSize(9).Bold().FontColor("#9CEAFF");
                            total.Item().AlignCenter().Text(importeTexto)
                                .Bold().FontSize(27).FontColor("#FFFFFF");
                        });

                    column.Item().PaddingTop(12).Text("Se recibió el importe indicado.")
                        .FontSize(10).FontColor("#DBF5FF");
                    column.Item().Text($"N.º de operación: {pago.Id:N}")
                        .FontSize(8).FontColor("#9AC5D8");
                    column.Item().PaddingTop(5)
                        .BorderTop(1).BorderColor("#2B7798")
                        .PaddingTop(10)
                        .Text("Este comprobante acredita el pago recibido, no necesariamente la cancelación total de las cuotas.")
                        .FontSize(8).FontColor("#AFCFDD");
                });
            });
        });
        documento.GeneratePdf(archivo);
        return (archivo.ToArray(), NombreRecibo(nombreAlumno, pago.Imputaciones.Select(x => x.Cuota.Periodo)) + ".pdf");
    }

    public static string NombreRecibo(string nombre, IEnumerable<DateOnly> periodos)
    {
        var meses = periodos.Distinct().OrderBy(x => x).Select(x => x.ToString("MM-yyyy")).ToArray();
        var texto = nombre.Trim() + " Comprobante Pago " + (meses.Length == 0 ? "sin periodo imputado" : "periodo " + string.Join("_", meses));
        var prohibidos = "<>:\"/\\|?*";
        return string.Concat(texto.Select(c => char.IsControl(c) || prohibidos.Contains(c) ? '_' : c)).Trim();
    }

    public async Task<(FileStream Contenido, string NombreArchivo)> GenerarMesAsync(CancellationToken ct = default)
    {
        var hoy = BebesNadadores.Domain.Common.FechaArgentina.Hoy;
        var inicio = new DateTimeOffset(hoy.Year, hoy.Month, 1, 0, 0, 0, TimeSpan.FromHours(-3)).ToUniversalTime();
        var fin = inicio.ToOffset(TimeSpan.FromHours(-3)).AddMonths(1).ToUniversalTime();
        await using var db = await factory.CreateDbContextAsync(ct);
        var ids = await db.Pagos.AsNoTracking().Where(x => x.AnuladoUtc == null && x.FechaUtc >= inicio && x.FechaUtc < fin)
            .OrderBy(x => x.FechaUtc).Select(x => x.Id).ToListAsync(ct);
        var stream = new FileStream(Path.Combine(Path.GetTempPath(), $"bbn-recibos-{Guid.NewGuid():N}.zip"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        try
        {
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in ids)
            {
                ct.ThrowIfCancellationRequested();
                var archivo = await GenerarAsync(id, ct);
                if (archivo is null) continue;
                var nombre = archivo.Value.NombreArchivo;
                for (var n = 2; !usados.Add(nombre); n++) nombre = Path.GetFileNameWithoutExtension(archivo.Value.NombreArchivo) + $" ({n}).pdf";
                var entry = zip.CreateEntry(nombre);
                await using var destino = entry.Open();
                await destino.WriteAsync(archivo.Value.Contenido, ct);
            }
        }
        stream.Position = 0;
        return (stream, $"Comprobantes {hoy:MM-yyyy}.zip");
        }
        catch { await stream.DisposeAsync(); throw; }
    }

    private static TimeZoneInfo ZonaArgentina()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time"); }
    }
}

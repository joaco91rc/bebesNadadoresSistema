using BebesNadadores.Application.Reportes.Dtos;

namespace BebesNadadores.Application.Reportes;

public interface IReporteExportService
{
    Task<ReporteArchivoDto> ExportarExcelAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default);

    Task<ReporteArchivoDto> ExportarPdfAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default);
}

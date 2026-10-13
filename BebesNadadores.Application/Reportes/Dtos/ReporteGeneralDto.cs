namespace BebesNadadores.Application.Reportes.Dtos;

public sealed record ReporteGeneralDto(
    DateOnly Desde,
    DateOnly Hasta,
    ReporteResumenDto Resumen,
    IReadOnlyList<AsistenciaDiaReporteDto> AsistenciaPorDia,
    IReadOnlyList<OcupacionNivelReporteDto> OcupacionPorNivel,
    RecuperosReporteDto Recuperos,
    FinanzasReporteDto Finanzas,
    CajaReporteDto Caja,
    BancosReporteDto Bancos,
    SaludReporteDto Salud);

public sealed record ReporteResumenDto(
    int BebesActivos,
    int AltasPeriodo,
    int ClasesDictables,
    int ClasesCanceladas,
    decimal AsistenciaPromedio,
    decimal OcupacionPromedio,
    decimal CobradoPeriodo,
    decimal DeudaActual);

public sealed record AsistenciaDiaReporteDto(
    DateOnly Fecha,
    int Presentes,
    int Ausentes,
    int ConAviso,
    int Pendientes,
    int Total,
    decimal PorcentajeAsistencia);

public sealed record OcupacionNivelReporteDto(
    Guid NivelId,
    string Nivel,
    int Clases,
    int CuposTotales,
    int Ocupados,
    int Vacantes,
    decimal PorcentajeOcupacion);

public sealed record RecuperosReporteDto(
    int Generados,
    int Disponibles,
    int Reservados,
    int Utilizados,
    int Vencidos,
    int Anulados,
    IReadOnlyList<RecuperoTipoReporteDto> PorTipo);

public sealed record RecuperoTipoReporteDto(
    string Tipo,
    int Cantidad);

public sealed record FinanzasReporteDto(
    int CuotasPeriodo,
    decimal FacturadoPeriodo,
    int PagosPeriodo,
    decimal CobradoPeriodo,
    decimal DeudaActual,
    int BebesConDeuda,
    IReadOnlyList<PagoMedioReporteDto> PorMedio,
    IReadOnlyList<DeudorReporteDto> MayoresDeudores);

public sealed record PagoMedioReporteDto(
    string Medio,
    int Cantidad,
    decimal Importe);

public sealed record DeudorReporteDto(
    Guid BebeId,
    string Bebe,
    int CuotasPendientes,
    decimal Deuda);

public sealed record CajaReporteDto(
    int Movimientos,
    decimal Ingresos,
    decimal Egresos,
    decimal Neto,
    IReadOnlyList<CajaCategoriaReporteDto> PorCategoria);

public sealed record CajaCategoriaReporteDto(
    string Categoria,
    int Cantidad,
    decimal Ingresos,
    decimal Egresos);

public sealed record BancosReporteDto(
    int Movimientos,
    decimal Ingresos,
    decimal Egresos,
    decimal Neto,
    IReadOnlyList<CuentaBancariaReporteDto> Cuentas);

public sealed record CuentaBancariaReporteDto(
    Guid CuentaBancariaId,
    string Banco,
    string Cuenta,
    int Movimientos,
    decimal Ingresos,
    decimal Egresos,
    decimal Neto,
    IReadOnlyList<MovimientoBancarioReporteDto> Detalle);

public sealed record MovimientoBancarioReporteDto(
    Guid Id,
    DateTimeOffset FechaUtc,
    string Tipo,
    string Origen,
    string Concepto,
    string? Referencia,
    string? Observaciones,
    decimal Importe);

public sealed record SaludReporteDto(
    int SinApto,
    int Vigentes,
    int PorVencer,
    int Vencidos,
    int Observados,
    int NoAptos);

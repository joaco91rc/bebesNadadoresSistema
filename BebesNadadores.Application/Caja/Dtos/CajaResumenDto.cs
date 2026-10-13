using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Caja.Dtos;

public sealed record CajaResumenDto(
    Guid Id,
    DateOnly FechaOperativa,
    EstadoCaja Estado,
    DateTimeOffset FechaAperturaUtc,
    DateTimeOffset? FechaCierreUtc,
    decimal SaldoInicial,
    decimal IngresosEfectivo,
    decimal EgresosEfectivo,
    decimal SaldoEsperado,
    decimal? SaldoFinalDeclarado,
    decimal? Diferencia,
    Guid UsuarioAperturaId,
    string UsuarioApertura,
    Guid? UsuarioCierreId,
    string? UsuarioCierre,
    string? Observaciones);
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Caja.Dtos;

public sealed record MovimientoCajaDto(
    Guid Id,
    DateTimeOffset FechaUtc,
    TipoMovimientoCaja Tipo,
    Guid? ConceptoCajaId,
    string Concepto,
    string? Detalle,
    decimal Importe,
    string? Referencia,
    Guid? PagoId);
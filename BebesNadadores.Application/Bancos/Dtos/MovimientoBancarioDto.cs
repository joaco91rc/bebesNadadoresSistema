using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Bancos.Dtos;

public sealed record MovimientoBancarioDto(
    Guid Id,
    Guid CuentaBancariaId,
    string Cuenta,
    string Banco,
    DateTimeOffset FechaUtc,
    TipoMovimientoBancario Tipo,
    OrigenMovimientoBancario Origen,
    decimal Importe,
    string Concepto,
    string? ReferenciaExterna,
    string? Observaciones,
    Guid? PagoId,
    string? Bebe);
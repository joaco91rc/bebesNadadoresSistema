using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Pagos.Dtos;

public sealed record CuotaCuentaCorrienteDto(
    Guid Id,
    DateOnly Periodo,
    DateOnly FechaVencimiento,
    decimal Importe,
    decimal ImportePagado,
    decimal Saldo,
    EstadoCuota Estado,
    string? Descripcion);
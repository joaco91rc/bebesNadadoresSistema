namespace BebesNadadores.Application.Cuotas.Dtos;

public sealed record BonificacionCuotaDto(
    Guid Id,
    Guid BebeId,
    string Bebe,
    DateOnly Periodo,
    decimal Importe,
    string Motivo,
    DateTimeOffset FechaUtc,
    string Usuario,
    bool Anulada,
    string? MotivoAnulacion);

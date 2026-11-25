namespace BebesNadadores.Application.Cuotas.Dtos;

public sealed record GeneracionCuotaIndividualDto(
    Guid CuotaId,
    DateOnly Periodo,
    decimal Importe,
    bool YaExistia,
    string? Advertencia = null);

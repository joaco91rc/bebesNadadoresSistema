namespace BebesNadadores.Application.Bancos.Dtos;

public sealed record ImputacionPagoBancarioDto(
    Guid CuotaId,
    DateOnly Periodo,
    decimal Importe);
namespace BebesNadadores.Application.Cuotas.Dtos;

public sealed record GeneracionCuotasResultadoDto(
    DateOnly Periodo,
    int InscripcionesEvaluadas,
    int CuotasGeneradas,
    int CuotasYaExistentes,
    int SinTarifa,
    IReadOnlyList<string> Advertencias);
namespace BebesNadadores.Application.ControlDocente.Dtos;

public sealed record ControlDocenteDiaDto(
    DateOnly Fecha,
    int CantidadClases,
    int ProfesoresEsperados,
    int Confirmados,
    int Pendientes,
    IReadOnlyList<ClaseControlDocenteDto> Clases);
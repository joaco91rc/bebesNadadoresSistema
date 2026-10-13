namespace BebesNadadores.Application.AptosMedicos.Dtos;

public sealed record FranjaMedicaDto(
    TimeOnly HoraInicio,
    string Posicion,
    bool EsActual,
    IReadOnlyList<ClaseMedicaDto> Clases);
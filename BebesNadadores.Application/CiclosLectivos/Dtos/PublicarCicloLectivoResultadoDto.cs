namespace BebesNadadores.Application.CiclosLectivos.Dtos;

public sealed record PublicarCicloLectivoResultadoDto(
    int Turnos,
    int SesionesGeneradas,
    int SesionesYaExistentes);
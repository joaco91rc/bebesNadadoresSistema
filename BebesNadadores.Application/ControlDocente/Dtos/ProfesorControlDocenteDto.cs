namespace BebesNadadores.Application.ControlDocente.Dtos;

public sealed record ProfesorControlDocenteDto(
    Guid ProfesorId,
    string NombreCompleto,
    bool EsPrincipal,
    bool EsReemplazo,
    bool ControlConfirmado,
    bool DictoClase);
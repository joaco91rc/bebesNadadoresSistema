namespace BebesNadadores.Application.ControlDocente.Dtos;

public sealed class RegistrarReemplazoDocenteDto
{
    public Guid SesionClaseId { get; set; }

    public Guid ProfesorAusenteId { get; set; }

    public Guid ProfesorQueDictoId { get; set; }

    public Guid? UsuarioId { get; set; }
}
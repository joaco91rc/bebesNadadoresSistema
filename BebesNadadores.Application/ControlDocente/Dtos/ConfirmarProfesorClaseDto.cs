namespace BebesNadadores.Application.ControlDocente.Dtos;

public sealed class ConfirmarProfesorClaseDto
{
    public Guid SesionClaseId { get; set; }

    public Guid ProfesorId { get; set; }

    public bool DictoClase { get; set; }

    public Guid? UsuarioId { get; set; }
}
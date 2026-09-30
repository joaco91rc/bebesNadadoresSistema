namespace BebesNadadores.Application.Turnera.Dtos;

public sealed class ActualizarSesionClaseDto
{
    public IReadOnlyCollection<ProfesorTurnoInputDto>
        Profesores { get; set; }
            = [];
}
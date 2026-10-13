using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.AptosMedicos.Dtos;

public sealed class GuardarAptoMedicoDto
{
    public Guid BebeId { get; set; }

    public DateOnly FechaRevision { get; set; }

    public DateOnly VigenciaHasta { get; set; }

    public ResultadoAptoMedico Resultado { get; set; }

    public string? Observaciones { get; set; }
}
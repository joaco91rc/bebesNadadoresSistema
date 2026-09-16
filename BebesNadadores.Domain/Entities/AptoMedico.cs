using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class AptoMedico : EntidadAuditable
{
    public Guid BebeId { get; set; }
    public Bebe Bebe { get; set; } = null!;

    public DateOnly FechaRevision { get; set; }
    public DateOnly VigenciaHasta { get; set; }

    public ResultadoAptoMedico Resultado { get; set; } = ResultadoAptoMedico.Apto;
    public string? Observaciones { get; set; }

    public Guid MedicoUsuarioId { get; set; }

    public string? DocumentoStorageKey { get; set; }
    public string? DocumentoNombreOriginal { get; set; }

    public bool EstaVigente(DateOnly hoy) =>
        Resultado == ResultadoAptoMedico.Apto && hoy <= VigenciaHasta;
}

using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Turnera.Dtos;

public sealed record DiaNoLaborableDto(
    Guid Id,
    DateOnly Fecha,
    TipoDiaNoLaborable Tipo,
    string Descripcion,
    bool TodoElDia,
    TimeOnly? HoraDesde,
    TimeOnly? HoraHasta,
    bool GeneraRecupero,
    bool RecuperoSinVencimiento,
    bool Activo);


public sealed class GuardarDiaNoLaborableDto
{
    public DateOnly Fecha { get; set; }

    public TipoDiaNoLaborable Tipo { get; set; }

    public string Descripcion { get; set; }
        = string.Empty;

    public bool TodoElDia { get; set; }
        = true;

    public TimeOnly? HoraDesde { get; set; }

    public TimeOnly? HoraHasta { get; set; }

    public bool GeneraRecupero { get; set; }
        = true;

    public bool RecuperoSinVencimiento { get; set; }
        = true;
}
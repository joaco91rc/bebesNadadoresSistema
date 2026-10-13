namespace BebesNadadores.Application.Configuracion.Dtos;

public sealed record TarifaPlanDto(
    Guid Id,
    decimal PrecioMensual,
    DateOnly VigenciaDesde,
    DateOnly? VigenciaHasta,
    bool EsVigente);

public sealed record PlanTarifaDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    int VecesPorSemana,
    bool Activo,
    decimal? PrecioActual,
    DateOnly? TarifaVigenteDesde,
    DateOnly? TarifaVigenteHasta,
    int InscripcionesActivas,
    IReadOnlyList<TarifaPlanDto> Tarifas);

public sealed class GuardarPlanDto
{
    public Guid? Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int VecesPorSemana { get; set; } = 1;
    public bool Activo { get; set; } = true;
}

public sealed class GuardarTarifaPlanDto
{
    public Guid PlanId { get; set; }
    public decimal PrecioMensual { get; set; }
    public DateOnly VigenciaDesde { get; set; }
}

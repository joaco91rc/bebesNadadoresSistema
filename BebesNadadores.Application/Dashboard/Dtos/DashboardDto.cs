using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Dashboard.Dtos;

public sealed class DashboardDto
{
    public int BebesActivos { get; init; }
    public int AltasMes { get; init; }

    public int CuotasPendientes { get; init; }
    public decimal ImportePendiente { get; init; }

    public int RecuperosDisponibles { get; init; }
    public int RecuperosVencenProximos { get; init; }

    public int AptosVencidos { get; init; }
    public int AptosSinApto { get; init; }
    public int AptosPorVencer { get; init; }
    public int AptosObservados { get; init; }
    public int AptosNoAptos { get; init; }

    public int VacantesHoy { get; init; }
    public int ClasesConVacantesHoy { get; init; }

    public int BebesProximosSiete { get; init; }

    public int PorcentajeAsistenciaSemanal { get; init; }

    public IReadOnlyList<DashboardClaseDto> ClasesHoy { get; init; }
        = [];

    public IReadOnlyList<DashboardVacanteDto> Vacantes { get; init; }
        = [];

    public IReadOnlyList<DashboardAsistenciaDiaDto> AsistenciaSemanal { get; init; }
        = [];

    public DashboardCajaDto Caja { get; init; }
        = new();

    public int AlertasMedicas =>
        AptosVencidos
        + AptosSinApto
        + AptosPorVencer
        + AptosObservados
        + AptosNoAptos;

    public int AlertasTotal =>
        AlertasMedicas
        + RecuperosVencenProximos
        + BebesProximosSiete;
}

public sealed record DashboardClaseDto(
    Guid SesionClaseId,
    string Hora,
    string Duracion,
    string Nivel,
    string Profesor,
    string Cupo,
    string EstadoCss);

public sealed record DashboardVacanteDto(
    Guid SesionClaseId,
    string Dia,
    string Hora,
    string Nivel,
    int Lugares);

public sealed record DashboardAsistenciaDiaDto(
    string Dia,
    int Porcentaje);

public sealed class DashboardCajaDto
{
    public bool Existe { get; init; }
    public EstadoCaja? Estado { get; init; }
    public decimal SaldoInicial { get; init; }
    public decimal Ingresos { get; init; }
    public decimal Egresos { get; init; }
    public decimal SaldoEsperado { get; init; }

    public bool EstaAbierta =>
        Estado == EstadoCaja.Abierta;
}

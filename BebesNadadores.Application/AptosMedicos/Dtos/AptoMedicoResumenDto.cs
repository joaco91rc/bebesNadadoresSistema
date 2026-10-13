namespace BebesNadadores.Application.AptosMedicos.Dtos;

public sealed record AptoMedicoResumenDto(
    int TotalBebes,
    int SinApto,
    int Vigentes,
    int PorVencer,
    int Vencidos,
    int Observados,
    int NoAptos);
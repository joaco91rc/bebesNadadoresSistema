namespace BebesNadadores.Application.Caja.Dtos;

/*
 * Una reapertura de caja y el cierre que deshizo.
 */
public sealed record CajaReaperturaDto(
    DateTimeOffset FechaUtc,
    string Usuario,
    string Motivo,
    DateTimeOffset FechaCierreAnteriorUtc,
    string UsuarioCierreAnterior,
    decimal SaldoFinalDeclaradoAnterior);

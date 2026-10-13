using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Pagos.Dtos;

public sealed record BebePagoResumenDto(
    Guid BebeId,
    string NombreCompleto,
    EstadoBebe Estado,
    string? ResponsablePrincipal,
    decimal Deuda,
    decimal SaldoAFavor);
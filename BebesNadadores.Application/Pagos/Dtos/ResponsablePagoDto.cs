namespace BebesNadadores.Application.Pagos.Dtos;

public sealed record ResponsablePagoDto(
    Guid Id,
    string NombreCompleto,
    bool EsPrincipal);
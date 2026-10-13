using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Caja.Dtos;

public sealed record ConceptoCajaDto(
    Guid Id,
    string Nombre,
    TipoMovimientoCaja Tipo,
    bool Activo,
    int Orden);
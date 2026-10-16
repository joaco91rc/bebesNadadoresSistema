using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Pagos.Dtos;

public sealed record PagoCuentaCorrienteDto(
    Guid Id,
    DateTimeOffset FechaUtc,
    decimal Importe,
    decimal ImporteImputado,
    decimal SaldoNoImputado,
    MedioPago MedioPago,
    Guid? CuentaBancariaId,
    string? CuentaBancaria,
    Guid? CajaId,
    string? ReferenciaExterna,
    string? Observaciones,
    bool Anulado = false,
    string? MotivoAnulacion = null,
    DateTimeOffset? AnuladoUtc = null,
    string? RegistradoPor = null,
    string? AnuladoPor = null);
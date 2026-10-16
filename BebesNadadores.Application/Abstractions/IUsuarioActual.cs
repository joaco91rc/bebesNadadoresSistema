namespace BebesNadadores.Application.Abstractions;

/*
 * Quién está usando el sistema en este momento.
 *
 * Lo usa la auditoría (CreadoPor / ModificadoPor) y los logs.
 * Devuelve null cuando no hay un usuario (jobs automáticos, seeder).
 */
public interface IUsuarioActual
{
    Task<UsuarioActualDto?> ObtenerAsync(
        CancellationToken ct = default);
}


public sealed record UsuarioActualDto(
    Guid Id,
    string Nombre);

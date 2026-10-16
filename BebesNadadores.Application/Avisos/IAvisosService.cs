namespace BebesNadadores.Application.Avisos;

/*
 * Avisos de la campana de la barra superior.
 *
 * Se calculan en el momento a partir de los datos (no se guardan):
 * cuando se resuelve el problema, el aviso desaparece solo.
 * Cada usuario ve solo los avisos de lo que puede gestionar.
 */
public interface IAvisosService
{
    Task<IReadOnlyList<AvisoDto>> ObtenerAsync(
        AlcanceAvisos alcance,
        CancellationToken ct = default);
}


/*
 * Qué grupos de avisos corresponden al usuario (según su rol).
 */
public sealed record AlcanceAvisos(
    bool Caja,
    bool Administrativos,
    bool Medicos,
    bool Administracion);


public enum NivelAviso
{
    Info = 1,
    Advertencia = 2,
    Urgente = 3
}


public sealed record AvisoDto(
    string Clave,
    NivelAviso Nivel,
    string Titulo,
    string Detalle,
    string Url);

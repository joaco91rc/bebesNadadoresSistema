namespace BebesNadadores.Application.Reportes.Dtos;

public sealed record ReporteArchivoDto(
    byte[] Contenido,
    string NombreArchivo,
    string ContentType);

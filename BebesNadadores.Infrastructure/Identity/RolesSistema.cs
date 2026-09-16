namespace BebesNadadores.Infrastructure.Identity;

public static class RolesSistema
{
    public const string Administrador = "Administrador";
    public const string Administrativa = "Administrativa";
    public const string Medica = "Medica";
    public const string Profesor = "Profesor";
    public const string SoloLectura = "SoloLectura";

    public static readonly string[] Todos =
    [
        Administrador,
        Administrativa,
        Medica,
        Profesor,
        SoloLectura
    ];
}
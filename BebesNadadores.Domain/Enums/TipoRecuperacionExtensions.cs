namespace BebesNadadores.Domain.Enums;

public static class TipoRecuperacionExtensions
{
    /*
     * Texto para pantallas y reportes (un solo lugar).
     */
    public static string ObtenerTexto(
        this TipoRecuperacion tipo)
    {
        return tipo switch
        {
            TipoRecuperacion.AusenciaConAviso => "Ausencia con aviso",
            TipoRecuperacion.ClaseNoDictada => "Clase no dictada",
            TipoRecuperacion.InicioMitadDeMes => "Bonificación por inicio a mitad de mes",
            TipoRecuperacion.SaldoHistoricoVerificado => "Saldo histórico verificado",
            _ => tipo.ToString()
        };
    }
}

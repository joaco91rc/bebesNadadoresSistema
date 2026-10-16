using BebesNadadores.Application.Comunicaciones;
using System.Text;


namespace BebesNadadores.Infrastructure.Services.Comunicaciones;

public sealed class WhatsAppService
    : IWhatsAppService
{
   public string CrearUrl(
    string telefono,
    string mensaje)
{
    if (string.IsNullOrWhiteSpace(
        telefono))
    {
        throw new InvalidOperationException(
            "El responsable no tiene un número de WhatsApp cargado.");
    }


    if (string.IsNullOrWhiteSpace(
        mensaje))
    {
        throw new InvalidOperationException(
            "El mensaje de WhatsApp no puede estar vacío.");
    }


    var numero =
        NormalizarNumeroArgentina(
            telefono);


    if (string.IsNullOrWhiteSpace(
        numero))
    {
        throw new InvalidOperationException(
            "El número de WhatsApp no es válido.");
    }


    var mensajeCodificado =
        CodificarUtf8(
            mensaje.Trim());


    return
        $"https://web.whatsapp.com/send" +
        $"?phone={numero}" +
        $"&text={mensajeCodificado}";
}

    public string? CrearUrlChat(
        string? telefono)
    {
        if (string.IsNullOrWhiteSpace(
            telefono))
        {
            return null;
        }

        var numero =
            NormalizarNumeroArgentina(
                telefono);

        return string.IsNullOrWhiteSpace(numero)
            ? null
            : $"https://wa.me/{numero}";
    }


private static string CodificarUtf8(
    string texto)
{
    var bytes =
        Encoding.UTF8.GetBytes(
            texto);


    var sb =
        new StringBuilder();


    foreach (var b in bytes)
    {
        var c =
            (char)b;


        if (
            b >= (byte)'A' &&
            b <= (byte)'Z'

            ||

            b >= (byte)'a' &&
            b <= (byte)'z'

            ||

            b >= (byte)'0' &&
            b <= (byte)'9'

            ||

            c == '-'
            ||
            c == '_'
            ||
            c == '.'
            ||
            c == '~')
        {
            sb.Append(
                c);
        }
        else
        {
            sb.Append(
                '%');

            sb.Append(
                b.ToString("X2"));
        }
    }


    return sb.ToString();
}


    // =========================================================
    // NORMALIZAR NÚMERO
    // =========================================================

    private static string NormalizarNumeroArgentina(
        string telefono)
    {
        var numero =
            new string(
                telefono
                    .Where(char.IsDigit)
                    .ToArray());


        if (string.IsNullOrWhiteSpace(
            numero))
        {
            return string.Empty;
        }


        /*
         * Ejemplos admitidos:
         *
         * 2215551234
         * 02215551234
         * 542215551234
         * 5492215551234
         * +54 9 221 555-1234
         */


        // ---------------------------------------------
        // QUITAR 00 INTERNACIONAL
        // ---------------------------------------------

        if (numero.StartsWith(
            "00"))
        {
            numero =
                numero[2..];
        }


        // ---------------------------------------------
        // YA VIENE COMO 549...
        // ---------------------------------------------

        if (numero.StartsWith(
            "549"))
        {
            return numero;
        }


        // ---------------------------------------------
        // VIENE COMO 54...
        // ---------------------------------------------

        if (numero.StartsWith(
            "54"))
        {
            numero =
                numero[2..];


            /*
             * Si después del 54 viene un 9,
             * lo sacamos para volver a construirlo
             * de forma consistente.
             */
            if (numero.StartsWith(
                "9"))
            {
                numero =
                    numero[1..];
            }
        }


        // ---------------------------------------------
        // QUITAR 0 DEL CÓDIGO DE ÁREA
        // ---------------------------------------------

        if (numero.StartsWith(
            "0"))
        {
            numero =
                numero[1..];
        }


        /*
         * En Argentina para WhatsApp usamos:
         *
         * 54 + 9 + código de área + número
         */
        return
            $"549{numero}";
    }
}
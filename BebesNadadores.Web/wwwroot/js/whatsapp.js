let bebesNadadoresWhatsAppWindow = null;


/*
 * ¿Celular o tablet?
 * iPadOS se presenta como "Macintosh", por eso también miramos
 * si la pantalla es táctil.
 */
function esDispositivoMovilBebesNadadores() {
    const ua = navigator.userAgent || "";

    if (/Android|iPhone|iPad|iPod|Mobile|Opera Mini|IEMobile/i.test(ua)) {
        return true;
    }

    return /Macintosh/i.test(ua) && navigator.maxTouchPoints > 1;
}


/*
 * El servidor arma el link de WhatsApp Web
 * (https://web.whatsapp.com/send?phone=...&text=...).
 * En el celular ese link no abre la app: lo pasamos a wa.me,
 * que sí la abre.
 */
function convertirAWaMeBebesNadadores(url) {
    try {
        const original = new URL(url);

        if (original.hostname !== "web.whatsapp.com") {
            return url;
        }

        const telefono = original.searchParams.get("phone") || "";
        const texto = original.searchParams.get("text");

        return "https://wa.me/" + encodeURIComponent(telefono) +
            (texto ? "?text=" + encodeURIComponent(texto) : "");
    }
    catch {
        return url;
    }
}


window.abrirWhatsAppBebesNadadores = function (url) {

    // =========================================================
    // CELULAR / TABLET: abrir la app de WhatsApp
    // =========================================================

    if (esDispositivoMovilBebesNadadores()) {

        const urlApp = convertirAWaMeBebesNadadores(url);

        /*
         * Si el navegador bloquea la ventana nueva (pasa en
         * algunos celulares), navegamos directo: WhatsApp se
         * abre y al volver el sistema sigue ahí.
         */
        const ventana = window.open(urlApp, "_blank");

        if (!ventana) {
            window.location.href = urlApp;
        }

        return;
    }


    // =========================================================
    // PC: WhatsApp Web, siempre en la misma pestaña
    // =========================================================

    /*
     * Si todavía no abrimos una ventana,
     * o el usuario la cerró,
     * creamos una nueva.
     */
    if (
        bebesNadadoresWhatsAppWindow === null ||
        bebesNadadoresWhatsAppWindow.closed
    ) {
        bebesNadadoresWhatsAppWindow = window.open(
            url,
            "BebesNadadoresWhatsApp"
        );

        return;
    }


    /*
     * Si ya existe, reutilizamos ESA ventana.
     */
    try {
        bebesNadadoresWhatsAppWindow.location.href =
            url;

        bebesNadadoresWhatsAppWindow.focus();
    }
    catch {
        /*
         * Algunos navegadores pueden perder la referencia
         * después de navegar a otro dominio.
         *
         * En ese caso intentamos recuperar la ventana
         * mediante el nombre fijo.
         */
        bebesNadadoresWhatsAppWindow = window.open(
            url,
            "BebesNadadoresWhatsApp"
        );
    }
};
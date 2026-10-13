let bebesNadadoresWhatsAppWindow = null;

window.abrirWhatsAppBebesNadadores = function (url) {

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
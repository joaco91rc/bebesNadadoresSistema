// Modo por navegador (NO cambia la cuenta ni afecta a otros empleados).
// Valores: 'web' (una pestaña reutilizable) o 'app' (protocolo WhatsApp).
const BBN_WA_KEY = 'bbn.whatsapp.modo.v1';
let bebesNadadoresWhatsAppWindow = null;

function esDispositivoMovilBebesNadadores() {
    const ua = navigator.userAgent || '';
    return /Android|iPhone|iPad|iPod|Mobile|Opera Mini|IEMobile/i.test(ua)
        || (/Macintosh/i.test(ua) && navigator.maxTouchPoints > 1);
}

window.obtenerModoWhatsAppBebesNadadores = function () {
    try { return localStorage.getItem(BBN_WA_KEY) === 'app' ? 'app' : 'web'; }
    catch { return 'web'; }
};
window.establecerModoWhatsAppBebesNadadores = function (modo) {
    if (modo !== 'app' && modo !== 'web') throw new Error('Modo no válido');
    try { localStorage.setItem(BBN_WA_KEY, modo); }
    catch { /* Almacenamiento bloqueado; se usará WhatsApp Web. */ }
};

function normalizarUrlWhatsAppBebesNadadores(url) {
    try {
        const u = new URL(url, location.origin);
        if (u.protocol !== 'https:' || !['web.whatsapp.com', 'wa.me', 'api.whatsapp.com'].includes(u.hostname))
            return null;
        let numero = u.searchParams.get('phone') || '';
        if (u.hostname === 'wa.me') numero = u.pathname.replace(/^\//, '');
        if (numero && !/^\d{7,16}$/.test(numero)) return null;
        const texto = u.searchParams.get('text') || '';
        return { numero, texto };
    } catch { return null; }
}

window.abrirWhatsAppBebesNadadores = function (url) {
    const datos = normalizarUrlWhatsAppBebesNadadores(url);
    if (!datos) return false;

    if (esDispositivoMovilBebesNadadores()) {
        const wa = 'https://wa.me/' + datos.numero
            + (datos.texto ? '?text=' + encodeURIComponent(datos.texto) : '');
        // Sin pestañas adicionales en teléfonos/tablets.
        window.location.href = wa;
        return true;
    }

    if (window.obtenerModoWhatsAppBebesNadadores() === 'app') {
        // WhatsApp Desktop debe estar instalado y registrado para whatsapp://.
        // Se navega desde la MISMA pestaña, sin crear pestañas residuales.
        const params = new URLSearchParams();
        if (datos.numero) params.set('phone', datos.numero);
        if (datos.texto) params.set('text', datos.texto);
        window.location.href = 'whatsapp://send?' + params.toString();
        return true; // Solo indica que se intentó abrir, NO que se envió.
    }

    // WhatsApp Web: reutilizar UNA pestaña/ventana por puesto.
    // Mantener la referencia evita crear pestañas nuevas al cambiar de chat.
    try {
        if (bebesNadadoresWhatsAppWindow && !bebesNadadoresWhatsAppWindow.closed) {
            bebesNadadoresWhatsAppWindow.location.replace(url);
            bebesNadadoresWhatsAppWindow.focus();
            return true;
        }
    } catch { /* La pestaña podría ser de otro origen. */ }

    const tab = window.open(url, 'BebesNadadoresWhatsApp');
    if (!tab) return false;
    bebesNadadoresWhatsAppWindow = tab;
    try { tab.opener = null; } catch { /* Navegador restrictivo */ }
    tab.focus();
    return true;
};

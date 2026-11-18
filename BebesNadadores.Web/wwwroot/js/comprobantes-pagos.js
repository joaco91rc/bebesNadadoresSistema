// En móvil intenta compartir el PDF directo con WhatsApp / share sheet.
// En escritorio descarga el comprobante, copia el texto al portapapeles
// y abre WhatsApp Web para que el usuario adjunte el archivo descargado.
export async function compartirComprobante(url, nombreArchivo, mensaje) {
  const respuesta = await fetch(url, { credentials: "same-origin", cache: "no-store" });
  if (!respuesta.ok) throw new Error("No se encontró un pago vigente para el comprobante.");

  const bytes = await respuesta.blob();
  const archivo = new File([bytes], nombreArchivo, { type: "application/pdf" });

  if (navigator.share && navigator.canShare?.({ files: [archivo] })) {
    try {
      await navigator.share({
        files: [archivo],
        title: "Bebés Nadadores - Comprobante de pago",
        text: mensaje || "Te adjuntamos el recibo del pago abonado."
      });
      return "compartido";
    } catch (error) {
      if (error?.name === "AbortError") return "cancelado";
      // Se continúa con la descarga y apertura de WhatsApp Web.
    }
  }

  const temporal = URL.createObjectURL(bytes);
  const enlace = document.createElement("a");
  enlace.href = temporal;
  enlace.download = nombreArchivo;
  document.body.appendChild(enlace);
  enlace.click();
  enlace.remove();
  setTimeout(() => URL.revokeObjectURL(temporal), 60_000);

  if (mensaje) {
    try {
      await navigator.clipboard.writeText(mensaje);
    } catch {
      // Si el navegador no permite portapapeles no frenamos el flujo.
    }

    const texto = encodeURIComponent(mensaje);
    const esMobile = /Android|iPhone|iPad|iPod/i.test(navigator.userAgent || "");
    const urlWhatsApp = esMobile
      ? `https://wa.me/?text=${texto}`
      : `https://web.whatsapp.com/send?text=${texto}`;

    window.open(urlWhatsApp, "_blank", "noopener,noreferrer");
    return "descargado";
  }

  return "descargado";
}

// Abre WhatsApp Web con un enlace protegido; no descarga ni adjunta archivos.
// El usuario revisa el destinatario y confirma el envío.
export async function abrirWhatsAppComprobante(mensaje) {
  const mobile = /Android|iPhone|iPad|iPod/i.test(navigator.userAgent || "");
  const texto = encodeURIComponent(mensaje);
  const destino = mobile
    ? `https://wa.me/?text=${texto}`
    : `https://web.whatsapp.com/send?text=${texto}`;

  // Abrir ANTES de cualquier await para minimizar el bloqueo de pop-ups.
  const ventana = window.open(destino, "_blank");
  if (ventana) ventana.opener = null;

  try {
    await navigator.clipboard?.writeText(mensaje);
  } catch {
    // El portapapeles es una ayuda, no una condición para continuar.
  }

  return ventana !== null;
}

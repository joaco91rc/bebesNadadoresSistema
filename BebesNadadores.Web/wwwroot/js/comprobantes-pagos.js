// Compartir archivo con WhatsApp mediante la hoja de compartir del dispositivo.
// Navegadores de escritorio pueden no admitir compartir PDF: descarga segura en su lugar.
export async function compartirComprobante(url, nombreArchivo) {
  const respuesta = await fetch(url, { credentials: "same-origin", cache: "no-store" });
  if (!respuesta.ok) throw new Error("No se encontró un pago vigente para el comprobante.");

  const bytes = await respuesta.blob();
  const archivo = new File([bytes], nombreArchivo, { type: "application/pdf" });

  if (navigator.share && navigator.canShare?.({ files: [archivo] })) {
    try {
      await navigator.share({
        files: [archivo],
        title: "Bebés Nadadores - Comprobante de pago",
        text: "Te compartimos el comprobante del pago recibido."
      });
      return "compartido";
    } catch (error) {
      if (error?.name === "AbortError") return "cancelado";
      // Algunos navegadores anuncian canShare pero no permiten adjuntar el PDF.
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
  return "descargado";
}

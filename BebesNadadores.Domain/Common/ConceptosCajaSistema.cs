namespace BebesNadadores.Domain.Common;

public static class ConceptosCajaSistema
{
    public static readonly Guid CuotaNatatorioMensualId =
        Guid.Parse(
            "f1f22e85-72f1-4d15-9fa8-23b7f26b4ac1");

    public const string CuotaNatatorioMensual =
        "Cuota natatorio mensual";

    // Concepto específico para anticipos del próximo ciclo lectivo.
    public static readonly Guid ReservaVacanteId =
        Guid.Parse("9d40845a-5d89-4bc4-a021-3d70b4c82cb7");

    public const string ReservaVacante = "Reserva de vacante";


        public static readonly Guid PagoProveedorId =
    Guid.Parse(
        "7d97a4c5-f3d8-48a1-88fd-5cdddb208ef7");


public const string PagoProveedor =
    "Pago a proveedor";
}
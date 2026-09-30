using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class DiaNoLaborable : EntidadAuditable
{
    public DateOnly Fecha { get; set; }


    public TipoDiaNoLaborable Tipo { get; set; }


    public string Descripcion { get; set; }
        = string.Empty;


    /*
     * true:
     * cierra todo el día.
     *
     * false:
     * usamos HoraDesde / HoraHasta.
     */
    public bool TodoElDia { get; set; } = true;


    public TimeOnly? HoraDesde { get; set; }

    public TimeOnly? HoraHasta { get; set; }


    /*
     * Normalmente feriados/cierres generan recupero.
     */
    public bool GeneraRecupero { get; set; } = true;


    /*
     * Para los cierres del natatorio:
     * recupero sin vencimiento.
     */
    public bool RecuperoSinVencimiento { get; set; } = true;


    public bool Activo { get; set; } = true;


    public ICollection<SesionClase> SesionesAfectadas { get; set; }
        = new List<SesionClase>();
}
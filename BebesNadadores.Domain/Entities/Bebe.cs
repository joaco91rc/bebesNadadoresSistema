using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class Bebe : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public DateOnly FechaNacimiento { get; set; }
    public Sexo Sexo { get; set; } = Sexo.NoInformado;
    public string? Dni { get; set; }

    /*
     * Declaración realizada por la familia.
     * Nullable permite distinguir registros históricos sin dato.
     */
    public bool? PermanecioHospitalMasDe5Dias { get; set; }

    public string? DetalleHospitalizacion { get; set; }

    public bool? RecibeTratamiento { get; set; }

    public string? DetalleTratamiento { get; set; }

    public DateOnly FechaAlta { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public EstadoBebe Estado { get; set; } = EstadoBebe.Activo;
    public DateOnly? FechaBaja { get; set; }
    public string? MotivoBaja { get; set; }

    public Guid? NivelActualId { get; set; }
    public Nivel? NivelActual { get; set; }

    public ICollection<BebeResponsable> Responsables { get; set; } = new List<BebeResponsable>();
    public ICollection<Inscripcion> Inscripciones { get; set; } = new List<Inscripcion>();
    public ICollection<Asistencia> Asistencias { get; set; } = new List<Asistencia>();
    public ICollection<Recuperacion> Recuperaciones { get; set; } = new List<Recuperacion>();
    public ICollection<AptoMedico> AptosMedicos { get; set; } = new List<AptoMedico>();
    public ICollection<PreferenciaRecuperacion> PreferenciasRecuperacion { get; set; } =
        new List<PreferenciaRecuperacion>();

    public int EdadEnMeses(DateOnly hoy)
    {
        var meses = (hoy.Year - FechaNacimiento.Year) * 12 + hoy.Month - FechaNacimiento.Month;
        if (hoy.Day < FechaNacimiento.Day)
            meses--;

        return Math.Max(0, meses);
    }

    public bool AlcanzoEdadMaxima(DateOnly hoy) => EdadEnMeses(hoy) >= 84;

    public void DarDeBaja(DateOnly fecha, string motivo)
    {
        Estado = EstadoBebe.Baja;
        FechaBaja = fecha;
        MotivoBaja = motivo;
    }

    public void Reactivar()
    {
        Estado = EstadoBebe.Activo;
        FechaBaja = null;
        MotivoBaja = null;
    }
}

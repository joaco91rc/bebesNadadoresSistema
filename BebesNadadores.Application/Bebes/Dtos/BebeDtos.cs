using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Bebes.Dtos;

public sealed record ResponsableBebeDto(
    Guid ResponsableId,
    string Nombre,
    string Apellido,
    string NombreCompleto,
    string? Dni,
    string Telefono,
    string WhatsApp,
    string Email,
    string? Domicilio,
    RelacionResponsable Relacion,
    bool EsContactoPrincipal,
    bool AutorizaComunicaciones);


public sealed record BebeDto(
    Guid Id,
    string Nombre,
    string Apellido,
    string NombreCompleto,
    DateOnly FechaNacimiento,
    Sexo Sexo,
    string? Dni,
      bool? PermanecioHospitalMasDe5Dias,
    string? DetalleHospitalizacion,

    bool? RecibeTratamiento,
    string? DetalleTratamiento,
    DateOnly FechaAlta,
    EstadoBebe Estado,
    DateOnly? FechaBaja,
    string? MotivoBaja,
    Guid? NivelActualId,
    string? NivelActual,
    int EdadMeses,
    int EdadAnios,
    bool AlcanzoEdadMaxima,
    IReadOnlyList<ResponsableBebeDto> Responsables);


public sealed class GuardarBebeDto
{
    public string Nombre { get; set; } = string.Empty;

    public string Apellido { get; set; } = string.Empty;

    // Campo único en la interfaz; se mantiene el esquema Nombre/Apellido.
    public string NombreYApellido
    {
        get => string.Join(" ", new[] { Nombre, Apellido }.Where(x => !string.IsNullOrWhiteSpace(x)));
        set
        {
            var partes = (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Nombre = partes.Length > 1 ? string.Join(" ", partes[..^1]) : (partes.Length == 1 ? partes[0] : string.Empty);
            Apellido = partes.Length > 1 ? partes[^1] : string.Empty;
        }
    }

    public DateOnly FechaNacimiento { get; set; }

    public Sexo Sexo { get; set; } = Sexo.NoInformado;

    public string? Dni { get; set; }

   public bool? PermanecioHospitalMasDe5Dias { get; set; }

public string? DetalleHospitalizacion { get; set; }

public bool? RecibeTratamiento { get; set; }

public string? DetalleTratamiento { get; set; }

    public Guid? NivelActualId { get; set; }

    public List<GuardarResponsableBebeDto> Responsables { get; set; }    = [];
  public Guid? PlanId { get; set; }

public DateOnly FechaInicioInscripcion { get; set; }
    = DateOnly.FromDateTime(DateTime.Today);

public MedioPago MedioPagoPreferido { get; set; }
    = MedioPago.Transferencia;

public List<Guid> TurnosIds { get; set; } = [];

// Excepción autorizada expresamente por la administrativa. Los cupos siguen visibles.
public bool PermitirSobrecupo { get; set; }
public string? MotivoSobrecupo { get; set; }
public bool DefinirHorarioHabitualAhora { get; set; }
    = true;
public TipoDescuento? TipoDescuento { get; set; }

public decimal? ValorDescuento { get; set; }

public string? MotivoDescuento { get; set; }

public Guid? SesionInicialId { get; set; }

// La inscripción puede guardarse con cuota pendiente y sin registrar cobro.
public bool RegistrarPagoInicial { get; set; } = false;
// null mantiene sugerencia de la fecha, true media cuota, false completa.
public bool? CobrarMediaCuotaPrimerMes { get; set; }
public MedioPago MedioPagoInicial { get; set; } = MedioPago.Transferencia;
public Guid? CuentaBancariaId { get; set; }
}


public sealed class GuardarResponsableBebeDto
{
    public Guid? ResponsableId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Apellido { get; set; } = string.Empty;

    // Campo único en la interfaz; se mantiene el esquema Nombre/Apellido.
    public string NombreYApellido
    {
        get => string.Join(" ", new[] { Nombre, Apellido }.Where(x => !string.IsNullOrWhiteSpace(x)));
        set
        {
            var partes = (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Nombre = partes.Length > 1 ? string.Join(" ", partes[..^1]) : (partes.Length == 1 ? partes[0] : string.Empty);
            Apellido = partes.Length > 1 ? partes[^1] : string.Empty;
        }
    }

    public string? Dni { get; set; }

    public string Telefono { get; set; } = string.Empty;

    public string WhatsApp { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Domicilio { get; set; }

    public RelacionResponsable Relacion { get; set; }

    public bool EsContactoPrincipal { get; set; }

    public bool AutorizaComunicaciones { get; set; } = true;
}


public sealed class DarDeBajaBebeDto
{
    public DateOnly Fecha { get; set; }
        = DateOnly.FromDateTime(DateTime.Today);

    public string Motivo { get; set; } = string.Empty;
}


public sealed record CumpleaniosBebeDto(
    Guid BebeId,
    string Nombre,
    string Apellido,
    string NombreCompleto,
    DateOnly FechaNacimiento,
    int EdadQueCumple,
    bool EsHoy,
    int DiasFaltantes);
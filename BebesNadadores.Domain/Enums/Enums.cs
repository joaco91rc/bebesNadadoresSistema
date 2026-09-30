namespace BebesNadadores.Domain.Enums;

public enum Sexo
{
    NoInformado = 0,
    Femenino = 1,
    Masculino = 2,
    Otro = 3
}

public enum EstadoBebe
{
    Activo = 1,
    Baja = 2
}

public enum RelacionResponsable
{
    Madre = 1,
    Padre = 2,
    Tutor = 3,
    Otro = 4
}

public enum DiaSemana
{
    Lunes = 1,
    Martes = 2,
    Miercoles = 3,
    Jueves = 4,
    Viernes = 5,
    Sabado = 6,
    Domingo = 7
}

public enum EstadoInscripcion
{
    Activa = 1,
    Pausada = 2,
    Finalizada = 3,
    Cancelada = 4
}

public enum EstadoTurno
{
    Activo = 1,
    Suspendido = 2,
    Baja = 3
}

public enum EstadoSesionClase
{
    Programada = 1,
    Cancelada = 2,
    Finalizada = 3
}

public enum TipoDiaNoLaborable
{
    Feriado = 1,
    CierreNatatorio = 2,
    Mantenimiento = 3,
    EventoEspecial = 4
}

public enum EstadoAsistencia
{
    Pendiente = 1,
    Presente = 2,
    AusenteConAviso = 3,
    Ausente = 4
}
public enum TipoRecuperacion
{
    AusenciaConAviso = 1,
    ClaseNoDictada = 2
}

public enum EstadoRecuperacion
{
    Disponible = 1,
    Reservada = 2,
    Utilizada = 3,
    Vencida = 4,
    Anulada = 5
}

public enum FranjaHoraria
{
    Indistinta = 0,
    Manana = 1,
    Tarde = 2,
    Noche = 3
}

public enum EstadoListaEspera
{
    Activa = 1,
    Notificada = 2,
    Asignada = 3,
    Cancelada = 4,
    Vencida = 5
}

public enum ResultadoAptoMedico
{
    Apto = 1,
    NoApto = 2,
    Observado = 3
}

public enum EstadoCuota
{
    Pendiente = 1,
    Parcial = 2,
    Pagada = 3,
    Vencida = 4,
    Anulada = 5
}

public enum MedioPago
{
    Efectivo = 1,
    Transferencia = 2,
    Tarjeta = 3,
    MercadoPago = 4,
    Otro = 5
}

public enum EstadoCaja
{
    Abierta = 1,
    Cerrada = 2
}

public enum TipoMovimientoCaja
{
    Ingreso = 1,
    Egreso = 2
}

public enum CategoriaMovimientoCaja
{
    Cuota = 1,
    Insumo = 2,
    Kiosco = 3,
    Ajuste = 4,
    Otro = 5
}

public enum CanalNotificacion
{
    WhatsAppManual = 1,
    Email = 2,
    WhatsAppApi = 3
}

public enum EstadoInvitacionVacante
{
    Pendiente = 1,
    Enviada = 2,
    Aceptada = 3,
    Rechazada = 4,
    Vencida = 5,
    SinCupo = 6
}
public enum TipoDescuento
{
    Porcentaje = 1,
    MontoFijo = 2
}



public enum EstadoCicloLectivo
{
    Borrador = 1,
    Publicado = 2,
    Cerrado = 3
}
namespace BebesNadadores.Domain.Common;

public abstract class EntidadBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

public abstract class EntidadAuditable : EntidadBase
{
    public DateTimeOffset CreadoUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreadoPorUsuarioId { get; set; }

    public DateTimeOffset? ModificadoUtc { get; set; }
    public Guid? ModificadoPorUsuarioId { get; set; }
}

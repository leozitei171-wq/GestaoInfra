namespace SistemaGestaoInfra.Domain.Common;

/// <summary>
/// Entidade base com identificador e timestamps de auditoria.
/// </summary>
public abstract class EntidadeBase
{
    public int Id { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? AtualizadoEm { get; set; }
}

/// <summary>
/// Entidade que pertence a uma prefeitura (tenant). Base do isolamento multi-tenant.
/// </summary>
public abstract class EntidadeTenant : EntidadeBase
{
    public int PrefeituraId { get; set; }
}

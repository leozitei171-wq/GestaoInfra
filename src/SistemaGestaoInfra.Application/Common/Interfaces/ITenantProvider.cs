namespace SistemaGestaoInfra.Application.Common.Interfaces;

/// <summary>
/// Resolve a prefeitura (tenant) do contexto atual da requisição.
/// Usado pelo DbContext para aplicar o filtro global de isolamento.
/// </summary>
public interface ITenantProvider
{
    /// <summary>
    /// Id da prefeitura do usuário autenticado. Null quando não há usuário
    /// autenticado ou quando o usuário é admin global.
    /// </summary>
    int? PrefeituraId { get; }

    /// <summary>
    /// Indica que o filtro multi-tenant deve ser ignorado (admin global,
    /// seeds, jobs de manutenção).
    /// </summary>
    bool IgnorarFiltro { get; }
}

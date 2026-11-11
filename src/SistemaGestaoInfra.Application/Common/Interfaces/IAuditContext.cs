namespace SistemaGestaoInfra.Application.Common.Interfaces;

/// <summary>
/// Informações do ator da ação corrente (usuário, IP e origem), usadas para auditoria.
/// Fora de uma requisição (seed, jobs) os valores vêm vazios e a origem é "sistema".
/// </summary>
public interface IAuditContext
{
    string? UsuarioId { get; }
    string? UsuarioNome { get; }
    string? Ip { get; }

    /// <summary>Origem da ação: "web" em requisições, "sistema" em rotinas internas.</summary>
    string Modo { get; }
}

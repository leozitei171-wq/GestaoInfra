namespace SistemaGestaoInfra.Domain.Entities;

/// <summary>
/// Registro de auditoria: quem fez o quê, quando e com quais dados.
/// É append-only — o sistema não oferece edição nem exclusão de registros.
/// Não herda de EntidadeBase de propósito, para nunca auditar a si mesma.
/// </summary>
public class Auditoria
{
    public long Id { get; set; }

    /// <summary>Momento do evento, em UTC.</summary>
    public DateTime QuandoEm { get; set; } = DateTime.UtcNow;

    /// <summary>Prefeitura (tenant) do evento. Nulo para eventos sem tenant (ex.: login de usuário inexistente).</summary>
    public int? PrefeituraId { get; set; }

    public string? UsuarioId { get; set; }

    /// <summary>Nome do usuário no momento do evento (preservado mesmo se o usuário for alterado depois).</summary>
    public string? UsuarioNome { get; set; }

    /// <summary>Criou, Alterou, Excluiu, Login, LoginFalhou, Logout...</summary>
    public string Acao { get; set; } = string.Empty;

    public string Entidade { get; set; } = string.Empty;

    public string? EntidadeId { get; set; }

    /// <summary>Origem da ação (ex.: web, sistema).</summary>
    public string Modo { get; set; } = "web";

    public string? Ip { get; set; }

    /// <summary>Detalhes em JSON: valores novos, alterações (de/para) ou valores anteriores.</summary>
    public string? Payload { get; set; }
}

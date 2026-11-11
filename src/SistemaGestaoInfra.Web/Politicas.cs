using SistemaGestaoInfra.Domain.Enums;

namespace SistemaGestaoInfra.Web;

/// <summary>
/// Nomes das políticas de autorização da aplicação.
/// </summary>
public static class Politicas
{
    /// <summary>Gestão dos cadastros base, programação e auditoria: perfis Gestor e Admin.</summary>
    public const string GestaoCadastros = "GestaoCadastros";

    /// <summary>Execução em campo (iniciar, progresso, concluir): Admin, Gestor e Servidor.</summary>
    public const string Execucao = "Execucao";

    public static readonly string[] PerfisGestaoCadastros =
    [
        nameof(PerfilUsuario.Admin),
        nameof(PerfilUsuario.Gestor)
    ];

    public static readonly string[] PerfisExecucao =
    [
        nameof(PerfilUsuario.Admin),
        nameof(PerfilUsuario.Gestor),
        nameof(PerfilUsuario.Servidor)
    ];
}

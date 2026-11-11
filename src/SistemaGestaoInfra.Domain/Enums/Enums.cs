namespace SistemaGestaoInfra.Domain.Enums;

/// <summary>
/// Perfis de acesso do sistema (RBAC).
/// </summary>
public enum PerfilUsuario
{
    Admin = 1,
    Gestor = 2,
    Servidor = 3,
    Vereador = 4,
    Cidadao = 5
}

/// <summary>
/// Situação de uma atividade/demanda no seu ciclo de vida.
/// Códigos alinhados com a referência (ABERTA, PROG, EM_EXEC, ATRASADA, NEGADA, CONCL).
/// </summary>
public enum SituacaoAtividade
{
    Aberta = 1,
    Programada = 2,
    EmExecucao = 3,
    Atrasada = 4,
    Negada = 5,
    Concluida = 6
}

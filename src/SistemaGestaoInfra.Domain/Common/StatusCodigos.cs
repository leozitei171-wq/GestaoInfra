namespace SistemaGestaoInfra.Domain.Common;

/// <summary>
/// Códigos de status reservados do sistema. A lógica de negócio (dashboard,
/// motor de atrasos, cores do mapa) depende destes códigos, por isso eles não
/// podem ser renomeados nem excluídos.
/// </summary>
public static class StatusCodigos
{
    public const string Aberta = "ABERTA";
    public const string Programada = "PROG";
    public const string EmExecucao = "EM_EXEC";
    public const string Atrasada = "ATRASADA";
    public const string Negada = "NEGADA";
    public const string Concluida = "CONCL";

    public static readonly IReadOnlySet<string> Reservados = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Aberta, Programada, EmExecucao, Atrasada, Negada, Concluida
    };

    public static bool EhReservado(string codigo) => Reservados.Contains(codigo);
}

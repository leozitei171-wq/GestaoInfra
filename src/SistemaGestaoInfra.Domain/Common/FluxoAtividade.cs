namespace SistemaGestaoInfra.Domain.Common;

/// <summary>
/// Regras de transição do ciclo de vida da atividade. Centralizadas aqui para que a tela
/// (que mostra/esconde botões), o controller (que valida) e o motor de atraso usem a mesma regra.
///
/// ABERTA ──Programar──► PROG ──Iniciar──► EM_EXEC ──Concluir──► CONCL
///   │                    │                  │
///   └────Negar───────────┘                  └─(prazo vencido, automático)─► ATRASADA
///                                                                              │
///                                          Reprogramar ◄─────────────────────┘ (volta a PROG ou EM_EXEC)
/// </summary>
public static class FluxoAtividade
{
    /// <summary>Define (ou redefine) as datas previstas.</summary>
    public static bool PodeProgramar(string status) =>
        status is StatusCodigos.Aberta or StatusCodigos.Programada
               or StatusCodigos.EmExecucao or StatusCodigos.Atrasada;

    /// <summary>Marca o início da execução. Uma atividade atrasada ainda não iniciada também pode ser iniciada.</summary>
    public static bool PodeIniciar(string status, bool jaIniciada) =>
        status == StatusCodigos.Programada
        || (status == StatusCodigos.Atrasada && !jaIniciada);

    public static bool PodeAtualizarProgresso(string status, bool jaIniciada) =>
        status == StatusCodigos.EmExecucao
        || (status == StatusCodigos.Atrasada && jaIniciada);

    public static bool PodeConcluir(string status) =>
        status is StatusCodigos.EmExecucao or StatusCodigos.Atrasada;

    public static bool PodeNegar(string status) =>
        status is StatusCodigos.Aberta or StatusCodigos.Programada;

    /// <summary>
    /// Status resultante de uma (re)programação. Atividades já iniciadas permanecem em execução;
    /// as demais ficam como programadas.
    /// </summary>
    public static string StatusAposProgramar(string statusAtual, bool jaIniciada) =>
        statusAtual == StatusCodigos.EmExecucao || jaIniciada
            ? StatusCodigos.EmExecucao
            : StatusCodigos.Programada;

    /// <summary>O motor só marca como atrasadas atividades programadas ou em execução.</summary>
    public static bool PodeVirarAtrasada(string status) =>
        status is StatusCodigos.Programada or StatusCodigos.EmExecucao;
}

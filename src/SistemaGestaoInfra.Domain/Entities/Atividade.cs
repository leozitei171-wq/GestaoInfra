using SistemaGestaoInfra.Domain.Common;

namespace SistemaGestaoInfra.Domain.Entities;

/// <summary>
/// Atividade/Demanda — entidade central do sistema. Representa uma solicitação de
/// serviço de infraestrutura urbana com localização, tipo, status e evidências.
/// </summary>
public class Atividade : EntidadeTenant
{
    public string Descricao { get; set; } = string.Empty;

    // Classificação
    public int TipoAtividadeId { get; set; }
    public int? SetorId { get; set; }
    public int PrioridadeId { get; set; }
    public int StatusId { get; set; }

    // Localização
    public string? Rua { get; set; }
    public string? Numero { get; set; }
    public string? Bairro { get; set; }
    public string? Cep { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // Execução
    public int Progresso { get; set; }

    /// <summary>Datas previstas são apenas datas (sem hora), por isso DateOnly.</summary>
    public DateOnly? DataInicioPrevisto { get; set; }
    public DateOnly? DataFimPrevisto { get; set; }

    /// <summary>Quando alguém marcou o início da execução. Distingue "atrasada e já iniciada" de "atrasada sem iniciar".</summary>
    public DateTime? DataInicioReal { get; set; }
    public DateTime? DataConclusao { get; set; }

    /// <summary>Justificativa obrigatória quando a atividade é negada.</summary>
    public string? MotivoNegativa { get; set; }

    // Evidência
    public string? FotoPath { get; set; }

    // Requerimento
    public bool Requerimento { get; set; }
    public string? NumeroRequerimento { get; set; }
    public string? ArquivoRequerimentoPath { get; set; }

    // Autoria
    public string? UsuarioAberturaId { get; set; }

    // Navegação
    public Prefeitura? Prefeitura { get; set; }
    public TipoAtividade? TipoAtividade { get; set; }
    public Setor? Setor { get; set; }
    public Prioridade? Prioridade { get; set; }
    public StatusAtividade? Status { get; set; }
}

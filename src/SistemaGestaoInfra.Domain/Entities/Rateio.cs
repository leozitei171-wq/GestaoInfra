using SistemaGestaoInfra.Domain.Common;
using SistemaGestaoInfra.Domain.Enums;

namespace SistemaGestaoInfra.Domain.Entities;

/// <summary>Categoria de um custo a ratear (ex.: administrativo, combustível, depreciação).</summary>
public class TipoCusto : EntidadeTenant, ICadastroSimples
{
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
}

/// <summary>
/// Rateio: divisão de um valor total entre contratos ou atividades, proporcionalmente a um peso
/// (valor do contrato ou custo da atividade). Registro financeiro: depois de criado não é editado.
/// </summary>
public class Rateio : EntidadeTenant
{
    public string Descricao { get; set; } = string.Empty;
    public decimal ValorTotal { get; set; }
    public DateOnly DataRateio { get; set; }
    public ModoRateio Modo { get; set; }
    public int? TipoCustoId { get; set; }

    /// <summary>Usuário que criou o rateio.</summary>
    public string? CriadoPorId { get; set; }

    public TipoCusto? TipoCusto { get; set; }
    public ICollection<RateioItem> Itens { get; set; } = new List<RateioItem>();
}

/// <summary>Parcela de um rateio destinada a um contrato ou a uma atividade.</summary>
public class RateioItem : EntidadeTenant
{
    public int RateioId { get; set; }

    /// <summary>Preenchido no modo Contrato.</summary>
    public int? ContratoId { get; set; }

    /// <summary>Preenchido no modo Atividade.</summary>
    public int? AtividadeId { get; set; }

    /// <summary>Peso usado na proporção (valor do contrato ou custo da atividade) no momento do rateio.</summary>
    public decimal Base { get; set; }

    /// <summary>Participação no total, em %.</summary>
    public decimal Percentual { get; set; }

    /// <summary>Valor em reais atribuído a este item. A soma dos itens é exatamente o total do rateio.</summary>
    public decimal Valor { get; set; }

    public Rateio? Rateio { get; set; }
    public Contrato? Contrato { get; set; }
    public Atividade? Atividade { get; set; }
}

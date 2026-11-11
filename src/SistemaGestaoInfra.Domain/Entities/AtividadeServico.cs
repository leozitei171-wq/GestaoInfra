using SistemaGestaoInfra.Domain.Common;

namespace SistemaGestaoInfra.Domain.Entities;

/// <summary>
/// Serviço de um contrato executado em uma atividade. É o que gera o custo da atividade
/// e o consumo (% de uso) do contrato.
/// </summary>
public class AtividadeServico : EntidadeTenant
{
    public int AtividadeId { get; set; }
    public int ContratoServicoId { get; set; }

    /// <summary>Quantidade executada.</summary>
    public decimal Quantidade { get; set; }

    /// <summary>Valor unitário no momento do lançamento (preserva o histórico se o contrato mudar).</summary>
    public decimal ValorUnitario { get; set; }

    public Atividade? Atividade { get; set; }
    public ContratoServico? ContratoServico { get; set; }

    public decimal Custo => Quantidade * ValorUnitario;
}

/// <summary>Aviso gerado quando um contrato se aproxima do limite de uso ou do vencimento.</summary>
public class ContratoAlerta : EntidadeTenant
{
    public int ContratoId { get; set; }

    /// <summary>atencao, critico ou vencimento (ver <see cref="NivelAlerta"/>).</summary>
    public string Nivel { get; set; } = NivelAlerta.Atencao;

    public decimal PercentualUso { get; set; }
    public string Mensagem { get; set; } = string.Empty;
    public bool Lido { get; set; }

    public Contrato? Contrato { get; set; }
}

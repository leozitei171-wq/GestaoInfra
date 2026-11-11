using SistemaGestaoInfra.Domain.Common;

namespace SistemaGestaoInfra.Domain.Entities;

/// <summary>
/// Contrato com um fornecedor, com uma vigência e uma lista de serviços contratados.
/// </summary>
public class Contrato : EntidadeTenant
{
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;

    public int FornecedorId { get; set; }
    public int? SetorId { get; set; }

    public DateOnly DataInicio { get; set; }
    public DateOnly DataFim { get; set; }

    /// <summary>Soma de (quantidade x valor unitário) dos serviços. Recalculado ao salvar.</summary>
    public decimal ValorTotal { get; set; }

    /// <summary>Caminho do PDF do contrato no armazenamento de arquivos.</summary>
    public string? ArquivoPdfPath { get; set; }

    public bool Ativo { get; set; } = true;

    public Fornecedor? Fornecedor { get; set; }
    public Setor? Setor { get; set; }
    public ICollection<ContratoServico> Servicos { get; set; } = new List<ContratoServico>();
}

/// <summary>Item de um contrato: um serviço, sua unidade de medição, quantidade e preço.</summary>
public class ContratoServico : EntidadeTenant
{
    public int ContratoId { get; set; }
    public int TipoServicoId { get; set; }
    public int TipoMedicaoId { get; set; }

    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }

    /// <summary>Quantidade máxima que pode ser executada deste item (opcional). Nulo = sem limite além da contratada.</summary>
    public decimal? Limite { get; set; }

    public Contrato? Contrato { get; set; }
    public TipoServico? TipoServico { get; set; }
    public TipoMedicao? TipoMedicao { get; set; }

    public decimal ValorItem => Quantidade * ValorUnitario;
}

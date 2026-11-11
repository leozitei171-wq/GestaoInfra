using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaGestaoInfra.Web.Services;

namespace SistemaGestaoInfra.Web.Controllers;

public class ContratoItemViewModel
{
    /// <summary>Id do item já existente (0 para novo). Mantém a identidade ao editar.</summary>
    public int Id { get; set; }

    public int? TipoServicoId { get; set; }
    public int? TipoMedicaoId { get; set; }

    [ModelBinder(typeof(DecimalFlexivelModelBinder))]
    public decimal? Quantidade { get; set; }

    [ModelBinder(typeof(DecimalFlexivelModelBinder))]
    public decimal? ValorUnitario { get; set; }

    [ModelBinder(typeof(DecimalFlexivelModelBinder))]
    public decimal? Limite { get; set; }
}

public class ContratoFormViewModel
{
    public int Id { get; set; }
    public bool EhNovo => Id == 0;

    [Required(ErrorMessage = "Informe o código.")]
    [StringLength(60)]
    [Display(Name = "Código")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a descrição.")]
    [StringLength(500)]
    [Display(Name = "Descrição")]
    public string Descricao { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecione o fornecedor.")]
    [Display(Name = "Fornecedor")]
    public int? FornecedorId { get; set; }

    [Display(Name = "Setor (opcional)")]
    public int? SetorId { get; set; }

    [Required(ErrorMessage = "Informe o início da vigência.")]
    [Display(Name = "Início da vigência")]
    public DateOnly? DataInicio { get; set; }

    [Required(ErrorMessage = "Informe o fim da vigência.")]
    [Display(Name = "Fim da vigência")]
    public DateOnly? DataFim { get; set; }

    [Display(Name = "PDF do contrato")]
    public IFormFile? ArquivoPdf { get; set; }

    public bool TemPdf { get; set; }

    [Display(Name = "Ativo")]
    public bool Ativo { get; set; } = true;

    public List<ContratoItemViewModel> Itens { get; set; } = new();

    public IEnumerable<SelectListItem> Fornecedores { get; set; } = [];
    public IEnumerable<SelectListItem> Setores { get; set; } = [];
    public IEnumerable<SelectListItem> Servicos { get; set; } = [];
    public IEnumerable<SelectListItem> Medicoes { get; set; } = [];
}

public record ContratoLista(
    int Id, string Codigo, string Descricao, string Fornecedor,
    DateOnly DataInicio, DateOnly DataFim, decimal ValorTotal, decimal Consumido, bool Ativo, bool TemPdf)
{
    public decimal PercentualUso => ValorTotal <= 0 ? 0 : Math.Round(Consumido * 100 / ValorTotal, 2);
}

public record ContratoItemDetalhe(
    string Servico, string Medicao, decimal Quantidade, decimal ValorUnitario, decimal? Limite, decimal Executado)
{
    public decimal ValorItem => Quantidade * ValorUnitario;
    public decimal PercentualUso => Quantidade <= 0 ? 0 : Math.Round(Executado * 100 / Quantidade, 2);
}

public class ContratoDetalheViewModel
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string Fornecedor { get; set; } = string.Empty;
    public string? Setor { get; set; }
    public DateOnly DataInicio { get; set; }
    public DateOnly DataFim { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal Consumido { get; set; }
    public bool Ativo { get; set; }
    public bool TemPdf { get; set; }
    public IReadOnlyList<ContratoItemDetalhe> Itens { get; set; } = [];

    public decimal PercentualUso => ValorTotal <= 0 ? 0 : Math.Round(Consumido * 100 / ValorTotal, 2);
    public int DiasParaVencer => DataFim.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber;
}

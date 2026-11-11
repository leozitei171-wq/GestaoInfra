using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaGestaoInfra.Web.Services;

namespace SistemaGestaoInfra.Web.Models;

public class AtividadeFiltro
{
    public string? Busca { get; set; }
    public int? StatusId { get; set; }
    public int? TipoAtividadeId { get; set; }
    public int? PrioridadeId { get; set; }
    public DateTime? De { get; set; }
    public DateTime? Ate { get; set; }
    public int Pagina { get; set; } = 1;

    public const int TamanhoPagina = 15;
}

public record AtividadeListaItem(
    int Id,
    string Descricao,
    string Tipo,
    string? Setor,
    string Status,
    string CorStatus,
    string Prioridade,
    int Progresso,
    bool TemFoto,
    DateTime CriadoEm);

public class AtividadeListaViewModel
{
    public AtividadeFiltro Filtro { get; set; } = new();
    public IReadOnlyList<AtividadeListaItem> Itens { get; set; } = [];
    public int Total { get; set; }
    public int TotalPaginas => (int)Math.Ceiling(Total / (double)AtividadeFiltro.TamanhoPagina);

    public IEnumerable<SelectListItem> Status { get; set; } = [];
    public IEnumerable<SelectListItem> Tipos { get; set; } = [];
    public IEnumerable<SelectListItem> Prioridades { get; set; } = [];
}

public class AtividadeFormViewModel
{
    // Passo 1 — Onde / o quê
    [Required(ErrorMessage = "Selecione o tipo de atividade.")]
    [Display(Name = "Tipo de atividade")]
    public int? TipoAtividadeId { get; set; }

    [StringLength(255)]
    [Display(Name = "Rua / logradouro")]
    public string? Rua { get; set; }

    [StringLength(20)]
    [Display(Name = "Número")]
    public string? Numero { get; set; }

    [StringLength(255)]
    [Display(Name = "Bairro")]
    public string? Bairro { get; set; }

    [StringLength(9)]
    [RegularExpression(@"^\d{5}-?\d{3}$", ErrorMessage = "CEP inválido. Use o formato 00000-000.")]
    [Display(Name = "CEP")]
    public string? Cep { get; set; }

    [Range(-90, 90, ErrorMessage = "Latitude deve estar entre -90 e 90.")]
    [Display(Name = "Latitude")]
    [ModelBinder(typeof(DecimalFlexivelModelBinder))]
    public decimal? Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude deve estar entre -180 e 180.")]
    [Display(Name = "Longitude")]
    [ModelBinder(typeof(DecimalFlexivelModelBinder))]
    public decimal? Longitude { get; set; }

    // Passo 2 — Detalhes
    [Required(ErrorMessage = "Selecione a prioridade.")]
    [Display(Name = "Prioridade")]
    public int? PrioridadeId { get; set; }

    [Required(ErrorMessage = "Descreva a atividade.")]
    [StringLength(2000, MinimumLength = 5, ErrorMessage = "A descrição deve ter entre 5 e 2000 caracteres.")]
    [Display(Name = "Descrição")]
    public string Descricao { get; set; } = string.Empty;

    [Display(Name = "Requerimento")]
    public bool Requerimento { get; set; }

    [StringLength(60)]
    [Display(Name = "Número do requerimento")]
    public string? NumeroRequerimento { get; set; }

    // Passo 3 — Evidência (arquivos não entram no binding por DataAnnotations)
    public IFormFile? Foto { get; set; }
    public IFormFile? ArquivoRequerimento { get; set; }

    public IEnumerable<SelectListItem> Tipos { get; set; } = [];
    public IEnumerable<SelectListItem> Prioridades { get; set; } = [];
}

public class AtividadeDetalheViewModel
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Setor { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CorStatus { get; set; } = string.Empty;
    public string Prioridade { get; set; } = string.Empty;
    public int Progresso { get; set; }
    public string? Endereco { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool TemFoto { get; set; }
    public bool Requerimento { get; set; }
    public string? NumeroRequerimento { get; set; }
    public bool TemArquivoRequerimento { get; set; }
    public string? AbertoPor { get; set; }
    public DateTime CriadoEm { get; set; }

    // Ciclo de vida
    public string StatusCodigo { get; set; } = string.Empty;
    public DateOnly? DataInicioPrevisto { get; set; }
    public DateOnly? DataFimPrevisto { get; set; }
    public DateTime? DataInicioReal { get; set; }
    public DateTime? DataConclusao { get; set; }
    public string? MotivoNegativa { get; set; }

    public bool JaIniciada => DataInicioReal is not null;

    // Serviços de contrato executados nesta atividade (geram o custo)
    public IReadOnlyList<AtividadeServicoLinha> Servicos { get; set; } = [];
    public IReadOnlyList<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> OpcoesServico { get; set; } = [];
    public decimal CustoTotal => Servicos.Sum(s => s.Custo);
}

public record AtividadeServicoLinha(
    int Id, string Contrato, string Servico, string Medicao, decimal Quantidade, decimal ValorUnitario)
{
    public decimal Custo => Quantidade * ValorUnitario;
}

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SistemaGestaoInfra.Web.Models;

/// <summary>Campos comuns aos cadastros base (código + descrição + situação).</summary>
public abstract class CadastroBaseViewModel
{
    public int Id { get; set; }

    public bool EhNovo => Id == 0;

    [Required(ErrorMessage = "Informe o código.")]
    [StringLength(60)]
    [RegularExpression(@"^[A-Za-z0-9_\-]+$", ErrorMessage = "Use apenas letras, números, hífen ou sublinhado.")]
    [Display(Name = "Código")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a descrição.")]
    [StringLength(255)]
    [Display(Name = "Descrição")]
    public string Descricao { get; set; } = string.Empty;

    [Display(Name = "Ativo")]
    public bool Ativo { get; set; } = true;
}

public class SetorViewModel : CadastroBaseViewModel
{
}

public class TipoAtividadeViewModel : CadastroBaseViewModel
{
    [Display(Name = "Setor responsável")]
    public int? SetorId { get; set; }

    public IEnumerable<SelectListItem> Setores { get; set; } = [];
}

public class PrioridadeViewModel : CadastroBaseViewModel
{
    [Range(0, 999, ErrorMessage = "Informe um valor entre 0 e 999.")]
    [Display(Name = "Ordem de exibição")]
    public int Ordem { get; set; }
}

public class StatusAtividadeViewModel : CadastroBaseViewModel
{
    [Required(ErrorMessage = "Informe a cor.")]
    [RegularExpression("^#([0-9a-fA-F]{6})$", ErrorMessage = "Use uma cor hexadecimal (ex: #0f766e).")]
    [Display(Name = "Cor no mapa")]
    public string Cor { get; set; } = "#8592a3";

    /// <summary>
    /// Status padrão do sistema têm código imutável: a lógica de negócio
    /// (dashboard, motor de atrasos, mapa) depende desses códigos.
    /// </summary>
    public bool EhPadrao { get; set; }
}

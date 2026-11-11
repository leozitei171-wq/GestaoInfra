using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaGestaoInfra.Domain.Enums;

namespace SistemaGestaoInfra.Web.Models;

public record UsuarioListaItem(
    string Id,
    string NomeCompleto,
    string Login,
    string Email,
    PerfilUsuario Perfil,
    string? Prefeitura,
    bool Ativo);

public class UsuarioFormViewModel
{
    public string? Id { get; set; }

    public bool EhNovo => string.IsNullOrEmpty(Id);

    [Required(ErrorMessage = "Informe o nome completo.")]
    [StringLength(255)]
    [Display(Name = "Nome completo")]
    public string NomeCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o login.")]
    [StringLength(100)]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Use apenas letras, números, ponto, hífen ou sublinhado.")]
    [Display(Name = "Login")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(255)]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecione a prefeitura.")]
    [Display(Name = "Prefeitura")]
    public int? PrefeituraId { get; set; }

    [Required(ErrorMessage = "Selecione o perfil.")]
    [Display(Name = "Perfil")]
    public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Servidor;

    /// <summary>Obrigatória na criação; na edição, preencher apenas para redefinir.</summary>
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "A senha deve ter ao menos 8 caracteres.")]
    [Display(Name = "Senha")]
    public string? Senha { get; set; }

    [Display(Name = "Ativo")]
    public bool Ativo { get; set; } = true;

    public IEnumerable<SelectListItem> Prefeituras { get; set; } = [];
}

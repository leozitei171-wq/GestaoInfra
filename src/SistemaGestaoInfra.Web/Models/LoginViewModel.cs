using System.ComponentModel.DataAnnotations;

namespace SistemaGestaoInfra.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Informe o usuário.")]
    [Display(Name = "Usuário")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha")]
    public string Senha { get; set; } = string.Empty;

    public bool LembrarMe { get; set; }

    public string? ReturnUrl { get; set; }
}

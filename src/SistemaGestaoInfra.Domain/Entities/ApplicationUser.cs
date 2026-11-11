using Microsoft.AspNetCore.Identity;
using SistemaGestaoInfra.Domain.Enums;

namespace SistemaGestaoInfra.Domain.Entities;

/// <summary>
/// Usuário do sistema. Estende o IdentityUser do ASP.NET Identity com dados
/// de tenant (prefeitura) e perfil.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string NomeCompleto { get; set; } = string.Empty;
    public int? PrefeituraId { get; set; }
    public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Servidor;
    public bool Ativo { get; set; } = true;

    // Navegação
    public Prefeitura? Prefeitura { get; set; }
}

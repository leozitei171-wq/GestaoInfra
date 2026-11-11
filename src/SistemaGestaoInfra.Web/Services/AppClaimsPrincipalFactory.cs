using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SistemaGestaoInfra.Domain.Entities;

namespace SistemaGestaoInfra.Web.Services;

/// <summary>
/// Adiciona as claims de prefeitura (tenant), perfil e nome ao principal do usuário.
/// Por ser usada pelo Identity sempre que o cookie é emitido ou renovado, as claims
/// não se perdem na revalidação do security stamp.
/// </summary>
public class AppClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public const string ClaimNome = "nome_completo";

    public AppClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(TenantProvider.ClaimPerfil, user.Perfil.ToString()));
        identity.AddClaim(new Claim(ClaimNome, user.NomeCompleto));

        if (user.PrefeituraId is int prefeituraId)
        {
            identity.AddClaim(new Claim(TenantProvider.ClaimPrefeitura, prefeituraId.ToString()));
        }

        return identity;
    }
}

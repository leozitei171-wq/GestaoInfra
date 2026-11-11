using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SistemaGestaoInfra.Application.Common.Interfaces;
using SistemaGestaoInfra.Domain.Enums;

namespace SistemaGestaoInfra.Web.Services;

/// <summary>
/// Resolve o tenant (prefeitura) a partir das claims do usuário autenticado.
/// Admins têm o filtro multi-tenant desligado (visão global).
/// </summary>
/// <remarks>
/// As claims são lidas sob demanda (não no construtor): o DbContext pode ser
/// criado pela infraestrutura do Identity antes do principal estar disponível,
/// então avaliar na leitura garante o valor correto por requisição.
/// </remarks>
public class TenantProvider : ITenantProvider
{
    public const string ClaimPrefeitura = "prefeitura_id";
    public const string ClaimPerfil = "perfil";

    private readonly IHttpContextAccessor _accessor;

    public TenantProvider(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public int? PrefeituraId
    {
        get
        {
            var user = Usuario;
            if (user is null)
            {
                return null;
            }

            return int.TryParse(user.FindFirstValue(ClaimPrefeitura), out var id) ? id : null;
        }
    }

    public bool IgnorarFiltro
    {
        get
        {
            var perfil = Usuario?.FindFirstValue(ClaimPerfil);
            return Enum.TryParse<PerfilUsuario>(perfil, out var p) && p == PerfilUsuario.Admin;
        }
    }

    private ClaimsPrincipal? Usuario
    {
        get
        {
            var user = _accessor.HttpContext?.User;
            return user?.Identity?.IsAuthenticated == true ? user : null;
        }
    }
}

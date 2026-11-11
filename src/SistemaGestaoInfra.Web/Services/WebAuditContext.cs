using System.Security.Claims;
using SistemaGestaoInfra.Application.Common.Interfaces;

namespace SistemaGestaoInfra.Web.Services;

/// <summary>
/// Descreve o ator da ação corrente a partir da requisição HTTP.
/// Fora de uma requisição (rotinas em segundo plano, seed) o modo é "sistema".
/// </summary>
public class WebAuditContext : IAuditContext
{
    private readonly IHttpContextAccessor _accessor;

    public WebAuditContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private HttpContext? Http => _accessor.HttpContext;

    private ClaimsPrincipal? Usuario =>
        Http?.User.Identity?.IsAuthenticated == true ? Http.User : null;

    public string? UsuarioId => Usuario?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UsuarioNome =>
        Usuario?.FindFirstValue(AppClaimsPrincipalFactory.ClaimNome)
        ?? Usuario?.Identity?.Name
        ?? (Http is null ? "Sistema" : null);

    public string? Ip => Http?.Connection.RemoteIpAddress?.ToString();

    public string Modo => Http is null ? "sistema" : "web";
}

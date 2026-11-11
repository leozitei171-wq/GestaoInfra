using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SistemaGestaoInfra.Domain.Entities;
using SistemaGestaoInfra.Infra.Data;
using SistemaGestaoInfra.Web.Models;

namespace SistemaGestaoInfra.Web.Controllers;

[AllowAnonymous]
public class ContaController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;

    public ContaController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        AppDbContext db)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Aceita login por nome de usuário ou e-mail.
        var usuario = await _userManager.FindByNameAsync(model.Login)
            ?? await _userManager.FindByEmailAsync(model.Login);

        if (usuario is null || !usuario.Ativo)
        {
            await RegistrarAcessoAsync("LoginFalhou", usuario, model.Login,
                usuario is null ? "usuário inexistente" : "usuário inativo", ct);
            ModelState.AddModelError(string.Empty, "Usuário ou senha inválidos.");
            return View(model);
        }

        // Claims de prefeitura/perfil são adicionadas pela AppClaimsPrincipalFactory.
        var resultado = await _signInManager.PasswordSignInAsync(
            usuario, model.Senha, model.LembrarMe, lockoutOnFailure: true);

        if (resultado.IsLockedOut)
        {
            await RegistrarAcessoAsync("LoginBloqueado", usuario, model.Login, "conta bloqueada por tentativas", ct);
            ModelState.AddModelError(string.Empty, "Conta bloqueada temporariamente por excesso de tentativas. Tente novamente em alguns minutos.");
            return View(model);
        }

        if (!resultado.Succeeded)
        {
            await RegistrarAcessoAsync("LoginFalhou", usuario, model.Login, "senha incorreta", ct);
            ModelState.AddModelError(string.Empty, "Usuário ou senha inválidos.");
            return View(model);
        }

        await RegistrarAcessoAsync("Login", usuario, model.Login, null, ct);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario is not null)
        {
            await RegistrarAcessoAsync("Logout", usuario, usuario.UserName, null, ct);
        }

        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AcessoNegado() => View();

    /// <summary>
    /// Grava eventos de acesso. A senha digitada nunca é registrada; o login informado é
    /// truncado porque, em tentativas falhas, o usuário às vezes digita a senha no campo errado.
    /// </summary>
    private async Task RegistrarAcessoAsync(
        string acao, ApplicationUser? usuario, string? loginInformado, string? motivo, CancellationToken ct)
    {
        var informado = loginInformado is { Length: > 60 } ? loginInformado[..60] + "…" : loginInformado;

        _db.Auditorias.Add(new Auditoria
        {
            PrefeituraId = usuario?.PrefeituraId,
            UsuarioId = usuario?.Id,
            UsuarioNome = usuario?.NomeCompleto,
            Acao = acao,
            Entidade = "Sessao",
            EntidadeId = usuario?.Id,
            Modo = "web",
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Payload = System.Text.Json.JsonSerializer.Serialize(
                new { login = informado, motivo },
                new System.Text.Json.JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                })
        });

        await _db.SaveChangesAsync(ct);
    }
}

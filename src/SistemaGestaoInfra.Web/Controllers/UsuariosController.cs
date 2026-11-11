using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Entities;
using SistemaGestaoInfra.Domain.Enums;
using SistemaGestaoInfra.Infra.Data;
using SistemaGestaoInfra.Web.Models;

namespace SistemaGestaoInfra.Web.Controllers;

/// <summary>
/// Administração de usuários e perfis. Acesso restrito ao perfil Admin.
/// </summary>
[Authorize(Roles = nameof(PerfilUsuario.Admin))]
public class UsuariosController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;

    public UsuariosController(UserManager<ApplicationUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<IActionResult> Index(string? busca, CancellationToken ct)
    {
        var query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = $"%{busca.Trim()}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.NomeCompleto, termo)
                || EF.Functions.ILike(u.UserName!, termo)
                || EF.Functions.ILike(u.Email!, termo));
        }

        var usuarios = await query
            .OrderBy(u => u.NomeCompleto)
            .Select(u => new UsuarioListaItem(
                u.Id,
                u.NomeCompleto,
                u.UserName!,
                u.Email!,
                u.Perfil,
                u.Prefeitura != null ? u.Prefeitura.Nome : null,
                u.Ativo))
            .ToListAsync(ct);

        ViewData["Title"] = "Usuários e perfis";
        ViewData["Busca"] = busca;
        return View(usuarios);
    }

    [HttpGet]
    public async Task<IActionResult> Novo(CancellationToken ct)
    {
        var model = new UsuarioFormViewModel();
        await CarregarListasAsync(model, ct);
        model.PrefeituraId = model.Prefeituras.Count() == 1
            ? int.Parse(model.Prefeituras.First().Value)
            : null;
        ViewData["Title"] = "Novo usuário";
        return View("Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Novo(UsuarioFormViewModel model, CancellationToken ct)
    {
        model.Id = null;
        if (string.IsNullOrWhiteSpace(model.Senha))
        {
            ModelState.AddModelError(nameof(model.Senha), "Informe a senha inicial.");
        }

        if (!ModelState.IsValid)
        {
            return await RetornarFormAsync(model, "Novo usuário", ct);
        }

        var usuario = new ApplicationUser
        {
            UserName = model.Login.Trim(),
            Email = model.Email.Trim(),
            EmailConfirmed = true,
            NomeCompleto = model.NomeCompleto.Trim(),
            PrefeituraId = model.PrefeituraId,
            Perfil = model.Perfil,
            Ativo = model.Ativo
        };

        var resultado = await _userManager.CreateAsync(usuario, model.Senha!);
        if (!resultado.Succeeded)
        {
            AdicionarErros(resultado);
            return await RetornarFormAsync(model, "Novo usuário", ct);
        }

        await _userManager.AddToRoleAsync(usuario, model.Perfil.ToString());

        TempData["Sucesso"] = $"Usuário \"{usuario.NomeCompleto}\" criado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(string id, CancellationToken ct)
    {
        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is null)
        {
            return NotFound();
        }

        var model = new UsuarioFormViewModel
        {
            Id = usuario.Id,
            NomeCompleto = usuario.NomeCompleto,
            Login = usuario.UserName ?? string.Empty,
            Email = usuario.Email ?? string.Empty,
            PrefeituraId = usuario.PrefeituraId,
            Perfil = usuario.Perfil,
            Ativo = usuario.Ativo
        };

        await CarregarListasAsync(model, ct);
        ViewData["Title"] = "Editar usuário";
        return View("Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(string id, UsuarioFormViewModel model, CancellationToken ct)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is null)
        {
            return NotFound();
        }

        var ehOProprio = usuario.Id == _userManager.GetUserId(User);
        if (ehOProprio && (!model.Ativo || model.Perfil != PerfilUsuario.Admin))
        {
            ModelState.AddModelError(string.Empty, "Você não pode desativar nem remover o perfil Admin da sua própria conta.");
        }

        if (!ModelState.IsValid)
        {
            return await RetornarFormAsync(model, "Editar usuário", ct);
        }

        var perfilAnterior = usuario.Perfil;
        usuario.NomeCompleto = model.NomeCompleto.Trim();
        usuario.UserName = model.Login.Trim();
        usuario.Email = model.Email.Trim();
        usuario.PrefeituraId = model.PrefeituraId;
        usuario.Perfil = model.Perfil;
        usuario.Ativo = model.Ativo;

        var resultado = await _userManager.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            AdicionarErros(resultado);
            return await RetornarFormAsync(model, "Editar usuário", ct);
        }

        if (perfilAnterior != model.Perfil)
        {
            await _userManager.RemoveFromRoleAsync(usuario, perfilAnterior.ToString());
            await _userManager.AddToRoleAsync(usuario, model.Perfil.ToString());
        }

        if (!string.IsNullOrWhiteSpace(model.Senha))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
            var reset = await _userManager.ResetPasswordAsync(usuario, token, model.Senha);
            if (!reset.Succeeded)
            {
                AdicionarErros(reset);
                return await RetornarFormAsync(model, "Editar usuário", ct);
            }
        }

        // Invalida sessões abertas para que perfil/prefeitura/status novos valham já.
        await _userManager.UpdateSecurityStampAsync(usuario);

        TempData["Sucesso"] = $"Usuário \"{usuario.NomeCompleto}\" atualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlternarAtivo(string id)
    {
        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is null)
        {
            return NotFound();
        }

        if (usuario.Id == _userManager.GetUserId(User))
        {
            TempData["Erro"] = "Você não pode desativar a sua própria conta.";
            return RedirectToAction(nameof(Index));
        }

        usuario.Ativo = !usuario.Ativo;
        await _userManager.UpdateAsync(usuario);
        await _userManager.UpdateSecurityStampAsync(usuario);

        TempData["Sucesso"] = usuario.Ativo
            ? $"Usuário \"{usuario.NomeCompleto}\" ativado."
            : $"Usuário \"{usuario.NomeCompleto}\" desativado.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> RetornarFormAsync(UsuarioFormViewModel model, string titulo, CancellationToken ct)
    {
        await CarregarListasAsync(model, ct);
        ViewData["Title"] = titulo;
        return View("Form", model);
    }

    private async Task CarregarListasAsync(UsuarioFormViewModel model, CancellationToken ct)
    {
        model.Prefeituras = await _db.Prefeituras
            .AsNoTracking()
            .Where(p => p.Ativo)
            .OrderBy(p => p.Nome)
            .Select(p => new SelectListItem($"{p.Nome} — {p.Cidade}/{p.Uf}", p.Id.ToString()))
            .ToListAsync(ct);
    }

    private void AdicionarErros(IdentityResult resultado)
    {
        foreach (var erro in resultado.Errors)
        {
            ModelState.AddModelError(string.Empty, TraduzirErro(erro));
        }
    }

    private static string TraduzirErro(IdentityError erro) => erro.Code switch
    {
        "DuplicateUserName" => "Já existe um usuário com este login.",
        "DuplicateEmail" => "Já existe um usuário com este e-mail.",
        "InvalidUserName" => "Login inválido.",
        "InvalidEmail" => "E-mail inválido.",
        "PasswordTooShort" => "A senha deve ter ao menos 8 caracteres.",
        "PasswordRequiresDigit" => "A senha deve conter ao menos um número.",
        "PasswordRequiresLower" => "A senha deve conter ao menos uma letra minúscula.",
        "PasswordRequiresUpper" => "A senha deve conter ao menos uma letra maiúscula.",
        _ => erro.Description
    };
}

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Common;
using SistemaGestaoInfra.Domain.Entities;
using SistemaGestaoInfra.Infra.Data;

namespace SistemaGestaoInfra.Web.Controllers;

public class FornecedorViewModel
{
    public int Id { get; set; }
    public bool EhNovo => Id == 0;

    [Required(ErrorMessage = "Informe a razão social.")]
    [StringLength(255)]
    [Display(Name = "Razão social")]
    public string RazaoSocial { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o CNPJ.")]
    [StringLength(18)]
    [Display(Name = "CNPJ")]
    public string Cnpj { get; set; } = string.Empty;

    [StringLength(255)]
    [Display(Name = "Contato (telefone/e-mail)")]
    public string? Contato { get; set; }

    [Display(Name = "Ativo")]
    public bool Ativo { get; set; } = true;
}

[Authorize(Policy = Politicas.GestaoCadastros)]
public class FornecedoresController : Controller
{
    private readonly AppDbContext _db;

    public FornecedoresController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Fornecedores";
        return View(await _db.Fornecedores.AsNoTracking().OrderBy(f => f.RazaoSocial).ToListAsync(ct));
    }

    [HttpGet]
    public IActionResult Novo()
    {
        ViewData["Title"] = "Novo fornecedor";
        return View("Form", new FornecedorViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Novo(FornecedorViewModel model, CancellationToken ct)
    {
        model.Id = 0;
        await ValidarAsync(model, ct);
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Novo fornecedor";
            return View("Form", model);
        }

        _db.Fornecedores.Add(new Fornecedor
        {
            RazaoSocial = model.RazaoSocial.Trim(),
            Cnpj = Cnpj.Normalizar(model.Cnpj),
            Contato = model.Contato?.Trim(),
            Ativo = model.Ativo
        });
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = "Fornecedor criado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id, CancellationToken ct)
    {
        var f = await _db.Fornecedores.FindAsync([id], ct);
        if (f is null) return NotFound();

        ViewData["Title"] = "Editar fornecedor";
        return View("Form", new FornecedorViewModel
        {
            Id = f.Id, RazaoSocial = f.RazaoSocial, Cnpj = Cnpj.Formatar(f.Cnpj), Contato = f.Contato, Ativo = f.Ativo
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, FornecedorViewModel model, CancellationToken ct)
    {
        if (id != model.Id) return BadRequest();

        var f = await _db.Fornecedores.FindAsync([id], ct);
        if (f is null) return NotFound();

        await ValidarAsync(model, ct);
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Editar fornecedor";
            return View("Form", model);
        }

        f.RazaoSocial = model.RazaoSocial.Trim();
        f.Cnpj = Cnpj.Normalizar(model.Cnpj);
        f.Contato = model.Contato?.Trim();
        f.Ativo = model.Ativo;
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = "Fornecedor atualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Excluir(int id, CancellationToken ct)
    {
        var f = await _db.Fornecedores.FindAsync([id], ct);
        if (f is null) return NotFound();

        if (await _db.Contratos.AnyAsync(c => c.FornecedorId == id, ct))
        {
            TempData["Erro"] = $"O fornecedor \"{f.RazaoSocial}\" possui contratos e não pode ser excluído. Desative-o em vez de excluir.";
            return RedirectToAction(nameof(Index));
        }

        _db.Fornecedores.Remove(f);
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = "Fornecedor excluído.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidarAsync(FornecedorViewModel model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Cnpj)) return;

        if (!Cnpj.Valido(model.Cnpj))
        {
            ModelState.AddModelError(nameof(model.Cnpj), "CNPJ inválido.");
            return;
        }

        var normalizado = Cnpj.Normalizar(model.Cnpj);
        if (await _db.Fornecedores.AnyAsync(f => f.Cnpj == normalizado && f.Id != model.Id, ct))
        {
            ModelState.AddModelError(nameof(model.Cnpj), "Já existe um fornecedor com este CNPJ.");
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Entities;
using SistemaGestaoInfra.Infra.Data;
using SistemaGestaoInfra.Web.Models;

namespace SistemaGestaoInfra.Web.Controllers;

[Authorize(Policy = Politicas.GestaoCadastros)]
public class SetoresController : Controller
{
    private readonly AppDbContext _db;

    public SetoresController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Setores";
        return View(await _db.Setores.AsNoTracking().OrderBy(s => s.Codigo).ToListAsync(ct));
    }

    [HttpGet]
    public IActionResult Novo()
    {
        ViewData["Title"] = "Novo setor";
        return View("Form", new SetorViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Novo(SetorViewModel model, CancellationToken ct)
    {
        model.Id = 0;
        await ValidarCodigoAsync(model, ct);

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Novo setor";
            return View("Form", model);
        }

        _db.Setores.Add(new Setor
        {
            Codigo = model.Codigo.Trim().ToUpperInvariant(),
            Descricao = model.Descricao.Trim(),
            Ativo = model.Ativo
        });
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = "Setor criado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id, CancellationToken ct)
    {
        var setor = await _db.Setores.FindAsync([id], ct);
        if (setor is null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Editar setor";
        return View("Form", new SetorViewModel
        {
            Id = setor.Id,
            Codigo = setor.Codigo,
            Descricao = setor.Descricao,
            Ativo = setor.Ativo
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, SetorViewModel model, CancellationToken ct)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var setor = await _db.Setores.FindAsync([id], ct);
        if (setor is null)
        {
            return NotFound();
        }

        await ValidarCodigoAsync(model, ct);

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Editar setor";
            return View("Form", model);
        }

        setor.Codigo = model.Codigo.Trim().ToUpperInvariant();
        setor.Descricao = model.Descricao.Trim();
        setor.Ativo = model.Ativo;
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = "Setor atualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Excluir(int id, CancellationToken ct)
    {
        var setor = await _db.Setores.FindAsync([id], ct);
        if (setor is null)
        {
            return NotFound();
        }

        // Setor em uso não pode ser removido; orientamos a desativação.
        var emUso = await _db.TiposAtividade.AnyAsync(t => t.SetorId == id, ct)
            || await _db.Atividades.AnyAsync(a => a.SetorId == id, ct);

        if (emUso)
        {
            TempData["Erro"] = $"O setor \"{setor.Codigo}\" está em uso e não pode ser excluído. Desative-o em vez de excluir.";
            return RedirectToAction(nameof(Index));
        }

        _db.Setores.Remove(setor);
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = $"Setor \"{setor.Codigo}\" excluído.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidarCodigoAsync(SetorViewModel model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Codigo))
        {
            return;
        }

        var codigo = model.Codigo.Trim().ToUpperInvariant();
        var duplicado = await _db.Setores
            .AnyAsync(s => s.Codigo.ToUpper() == codigo && s.Id != model.Id, ct);

        if (duplicado)
        {
            ModelState.AddModelError(nameof(model.Codigo), "Já existe um setor com este código.");
        }
    }
}

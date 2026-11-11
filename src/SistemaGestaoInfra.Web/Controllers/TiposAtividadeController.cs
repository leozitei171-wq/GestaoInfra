using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Entities;
using SistemaGestaoInfra.Infra.Data;
using SistemaGestaoInfra.Web.Models;

namespace SistemaGestaoInfra.Web.Controllers;

[Authorize(Policy = Politicas.GestaoCadastros)]
public class TiposAtividadeController : Controller
{
    private readonly AppDbContext _db;

    public TiposAtividadeController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Tipos de atividade";
        var itens = await _db.TiposAtividade
            .AsNoTracking()
            .Include(t => t.Setor)
            .OrderBy(t => t.Codigo)
            .ToListAsync(ct);
        return View(itens);
    }

    [HttpGet]
    public async Task<IActionResult> Novo(CancellationToken ct)
    {
        var model = new TipoAtividadeViewModel();
        await CarregarSetoresAsync(model, ct);
        ViewData["Title"] = "Novo tipo de atividade";
        return View("Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Novo(TipoAtividadeViewModel model, CancellationToken ct)
    {
        model.Id = 0;
        await ValidarCodigoAsync(model, ct);

        if (!ModelState.IsValid)
        {
            return await RetornarFormAsync(model, "Novo tipo de atividade", ct);
        }

        _db.TiposAtividade.Add(new TipoAtividade
        {
            Codigo = model.Codigo.Trim().ToUpperInvariant(),
            Descricao = model.Descricao.Trim(),
            SetorId = model.SetorId,
            Ativo = model.Ativo
        });
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = "Tipo de atividade criado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id, CancellationToken ct)
    {
        var item = await _db.TiposAtividade.FindAsync([id], ct);
        if (item is null)
        {
            return NotFound();
        }

        var model = new TipoAtividadeViewModel
        {
            Id = item.Id,
            Codigo = item.Codigo,
            Descricao = item.Descricao,
            SetorId = item.SetorId,
            Ativo = item.Ativo
        };
        await CarregarSetoresAsync(model, ct);

        ViewData["Title"] = "Editar tipo de atividade";
        return View("Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, TipoAtividadeViewModel model, CancellationToken ct)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var item = await _db.TiposAtividade.FindAsync([id], ct);
        if (item is null)
        {
            return NotFound();
        }

        await ValidarCodigoAsync(model, ct);

        if (!ModelState.IsValid)
        {
            return await RetornarFormAsync(model, "Editar tipo de atividade", ct);
        }

        item.Codigo = model.Codigo.Trim().ToUpperInvariant();
        item.Descricao = model.Descricao.Trim();
        item.SetorId = model.SetorId;
        item.Ativo = model.Ativo;
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = "Tipo de atividade atualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Excluir(int id, CancellationToken ct)
    {
        var item = await _db.TiposAtividade.FindAsync([id], ct);
        if (item is null)
        {
            return NotFound();
        }

        if (await _db.Atividades.AnyAsync(a => a.TipoAtividadeId == id, ct))
        {
            TempData["Erro"] = $"O tipo \"{item.Codigo}\" está em uso e não pode ser excluído. Desative-o em vez de excluir.";
            return RedirectToAction(nameof(Index));
        }

        _db.TiposAtividade.Remove(item);
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = $"Tipo \"{item.Codigo}\" excluído.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> RetornarFormAsync(TipoAtividadeViewModel model, string titulo, CancellationToken ct)
    {
        await CarregarSetoresAsync(model, ct);
        ViewData["Title"] = titulo;
        return View("Form", model);
    }

    private async Task CarregarSetoresAsync(TipoAtividadeViewModel model, CancellationToken ct)
    {
        model.Setores = await _db.Setores
            .AsNoTracking()
            .Where(s => s.Ativo)
            .OrderBy(s => s.Codigo)
            .Select(s => new SelectListItem($"{s.Codigo} — {s.Descricao}", s.Id.ToString()))
            .ToListAsync(ct);
    }

    private async Task ValidarCodigoAsync(TipoAtividadeViewModel model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Codigo))
        {
            return;
        }

        var codigo = model.Codigo.Trim().ToUpperInvariant();
        if (await _db.TiposAtividade.AnyAsync(t => t.Codigo.ToUpper() == codigo && t.Id != model.Id, ct))
        {
            ModelState.AddModelError(nameof(model.Codigo), "Já existe um tipo de atividade com este código.");
        }
    }
}

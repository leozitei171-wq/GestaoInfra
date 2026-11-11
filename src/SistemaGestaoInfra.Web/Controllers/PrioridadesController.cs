using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Entities;
using SistemaGestaoInfra.Infra.Data;
using SistemaGestaoInfra.Web.Models;

namespace SistemaGestaoInfra.Web.Controllers;

[Authorize(Policy = Politicas.GestaoCadastros)]
public class PrioridadesController : Controller
{
    private readonly AppDbContext _db;

    public PrioridadesController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Prioridades";
        var itens = await _db.Prioridades
            .AsNoTracking()
            .OrderBy(p => p.Ordem)
            .ThenBy(p => p.Codigo)
            .ToListAsync(ct);
        return View(itens);
    }

    [HttpGet]
    public async Task<IActionResult> Novo(CancellationToken ct)
    {
        var proximaOrdem = await _db.Prioridades.AnyAsync(ct)
            ? await _db.Prioridades.MaxAsync(p => p.Ordem, ct) + 1
            : 1;

        ViewData["Title"] = "Nova prioridade";
        return View("Form", new PrioridadeViewModel { Ordem = proximaOrdem });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Novo(PrioridadeViewModel model, CancellationToken ct)
    {
        model.Id = 0;
        await ValidarCodigoAsync(model, ct);

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Nova prioridade";
            return View("Form", model);
        }

        _db.Prioridades.Add(new Prioridade
        {
            Codigo = model.Codigo.Trim().ToUpperInvariant(),
            Descricao = model.Descricao.Trim(),
            Ordem = model.Ordem,
            Ativo = model.Ativo
        });
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = "Prioridade criada com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id, CancellationToken ct)
    {
        var item = await _db.Prioridades.FindAsync([id], ct);
        if (item is null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Editar prioridade";
        return View("Form", new PrioridadeViewModel
        {
            Id = item.Id,
            Codigo = item.Codigo,
            Descricao = item.Descricao,
            Ordem = item.Ordem,
            Ativo = item.Ativo
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, PrioridadeViewModel model, CancellationToken ct)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var item = await _db.Prioridades.FindAsync([id], ct);
        if (item is null)
        {
            return NotFound();
        }

        await ValidarCodigoAsync(model, ct);

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Editar prioridade";
            return View("Form", model);
        }

        item.Codigo = model.Codigo.Trim().ToUpperInvariant();
        item.Descricao = model.Descricao.Trim();
        item.Ordem = model.Ordem;
        item.Ativo = model.Ativo;
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = "Prioridade atualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Excluir(int id, CancellationToken ct)
    {
        var item = await _db.Prioridades.FindAsync([id], ct);
        if (item is null)
        {
            return NotFound();
        }

        if (await _db.Atividades.AnyAsync(a => a.PrioridadeId == id, ct))
        {
            TempData["Erro"] = $"A prioridade \"{item.Codigo}\" está em uso e não pode ser excluída. Desative-a em vez de excluir.";
            return RedirectToAction(nameof(Index));
        }

        _db.Prioridades.Remove(item);
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = $"Prioridade \"{item.Codigo}\" excluída.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidarCodigoAsync(PrioridadeViewModel model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Codigo))
        {
            return;
        }

        var codigo = model.Codigo.Trim().ToUpperInvariant();
        if (await _db.Prioridades.AnyAsync(p => p.Codigo.ToUpper() == codigo && p.Id != model.Id, ct))
        {
            ModelState.AddModelError(nameof(model.Codigo), "Já existe uma prioridade com este código.");
        }
    }
}

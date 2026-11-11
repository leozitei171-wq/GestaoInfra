using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Common;
using SistemaGestaoInfra.Domain.Entities;
using SistemaGestaoInfra.Infra.Data;
using SistemaGestaoInfra.Web.Models;

namespace SistemaGestaoInfra.Web.Controllers;

/// <summary>
/// Cadastro de status de atividade. Os status reservados (ver StatusCodigos)
/// sustentam regras de negócio: o código é imutável e eles não podem ser
/// excluídos nem desativados. Descrição e cor permanecem editáveis.
/// </summary>
[Authorize(Policy = Politicas.GestaoCadastros)]
public class StatusAtividadeController : Controller
{
    private readonly AppDbContext _db;

    public StatusAtividadeController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Status de atividade";
        var itens = await _db.StatusAtividades
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .ToListAsync(ct);
        return View(itens);
    }

    [HttpGet]
    public IActionResult Novo()
    {
        ViewData["Title"] = "Novo status";
        return View("Form", new StatusAtividadeViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Novo(StatusAtividadeViewModel model, CancellationToken ct)
    {
        model.Id = 0;
        model.EhPadrao = false;

        var codigo = model.Codigo?.Trim().ToUpperInvariant() ?? string.Empty;
        if (StatusCodigos.EhReservado(codigo))
        {
            ModelState.AddModelError(nameof(model.Codigo), "Este código é reservado pelo sistema. Escolha outro.");
        }

        await ValidarCodigoAsync(model, ct);

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Novo status";
            return View("Form", model);
        }

        _db.StatusAtividades.Add(new StatusAtividade
        {
            Codigo = codigo,
            Descricao = model.Descricao.Trim(),
            Cor = model.Cor.Trim(),
            Ativo = model.Ativo
        });
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = "Status criado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id, CancellationToken ct)
    {
        var item = await _db.StatusAtividades.FindAsync([id], ct);
        if (item is null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Editar status";
        return View("Form", new StatusAtividadeViewModel
        {
            Id = item.Id,
            Codigo = item.Codigo,
            Descricao = item.Descricao,
            Cor = item.Cor,
            Ativo = item.Ativo,
            EhPadrao = StatusCodigos.EhReservado(item.Codigo)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, StatusAtividadeViewModel model, CancellationToken ct)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var item = await _db.StatusAtividades.FindAsync([id], ct);
        if (item is null)
        {
            return NotFound();
        }

        var ehPadrao = StatusCodigos.EhReservado(item.Codigo);
        model.EhPadrao = ehPadrao;
        var codigoNormalizado = model.Codigo?.Trim().ToUpperInvariant() ?? string.Empty;

        if (ehPadrao)
        {
            // Código e situação dos status reservados são imutáveis,
            // independente do que vier no formulário.
            model.Codigo = item.Codigo;
            model.Ativo = true;
        }
        else
        {
            if (StatusCodigos.EhReservado(codigoNormalizado))
            {
                ModelState.AddModelError(nameof(model.Codigo), "Este código é reservado pelo sistema. Escolha outro.");
            }

            await ValidarCodigoAsync(model, ct);
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Editar status";
            return View("Form", model);
        }

        if (!ehPadrao)
        {
            item.Codigo = codigoNormalizado;
            item.Ativo = model.Ativo;
        }

        item.Descricao = model.Descricao.Trim();
        item.Cor = model.Cor.Trim();
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = "Status atualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Excluir(int id, CancellationToken ct)
    {
        var item = await _db.StatusAtividades.FindAsync([id], ct);
        if (item is null)
        {
            return NotFound();
        }

        if (StatusCodigos.EhReservado(item.Codigo))
        {
            TempData["Erro"] = $"O status \"{item.Codigo}\" é padrão do sistema e não pode ser excluído.";
            return RedirectToAction(nameof(Index));
        }

        if (await _db.Atividades.AnyAsync(a => a.StatusId == id, ct))
        {
            TempData["Erro"] = $"O status \"{item.Codigo}\" está em uso e não pode ser excluído. Desative-o em vez de excluir.";
            return RedirectToAction(nameof(Index));
        }

        _db.StatusAtividades.Remove(item);
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = $"Status \"{item.Codigo}\" excluído.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidarCodigoAsync(StatusAtividadeViewModel model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Codigo))
        {
            return;
        }

        var codigo = model.Codigo.Trim().ToUpperInvariant();
        if (await _db.StatusAtividades.AnyAsync(s => s.Codigo.ToUpper() == codigo && s.Id != model.Id, ct))
        {
            ModelState.AddModelError(nameof(model.Codigo), "Já existe um status com este código.");
        }
    }
}

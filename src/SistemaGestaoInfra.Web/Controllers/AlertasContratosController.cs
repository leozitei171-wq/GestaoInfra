using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Infra.Data;

namespace SistemaGestaoInfra.Web.Controllers;

public record AlertaLinha(
    int Id, DateTime Quando, int ContratoId, string Contrato, string Fornecedor,
    string Nivel, decimal PercentualUso, string Mensagem, bool Lido);

public class AlertasViewModel
{
    public IReadOnlyList<AlertaLinha> Itens { get; set; } = [];
    public int Total { get; set; }
    public int NaoLidos { get; set; }
    public int Pagina { get; set; } = 1;
    public bool SomenteNaoLidos { get; set; }
    public int TotalPaginas => (int)Math.Ceiling(Total / (double)AlertasContratosController.TamanhoPagina);
}

/// <summary>Alertas de uso e vencimento de contratos. Restrito a Gestor e Admin.</summary>
[Authorize(Policy = Politicas.GestaoCadastros)]
public class AlertasContratosController : Controller
{
    public const int TamanhoPagina = 20;

    private readonly AppDbContext _db;

    public AlertasContratosController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int pagina = 1, bool somenteNaoLidos = false, CancellationToken ct = default)
    {
        if (pagina < 1) pagina = 1;

        var todos = _db.ContratoAlertas.AsNoTracking();
        var filtrados = somenteNaoLidos ? todos.Where(a => !a.Lido) : todos;

        var itens = await filtrados
            .OrderBy(a => a.Lido).ThenByDescending(a => a.CriadoEm)
            .Skip((pagina - 1) * TamanhoPagina).Take(TamanhoPagina)
            .Select(a => new AlertaLinha(
                a.Id, a.CriadoEm, a.ContratoId, a.Contrato!.Codigo, a.Contrato.Fornecedor!.RazaoSocial,
                a.Nivel, a.PercentualUso, a.Mensagem, a.Lido))
            .ToListAsync(ct);

        ViewData["Title"] = "Alertas de contratos";
        return View(new AlertasViewModel
        {
            Itens = itens,
            Total = await filtrados.CountAsync(ct),
            NaoLidos = await todos.CountAsync(a => !a.Lido, ct),
            Pagina = pagina,
            SomenteNaoLidos = somenteNaoLidos
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarLidos(int[]? ids, CancellationToken ct)
    {
        if (ids is null || ids.Length == 0)
        {
            TempData["Erro"] = "Selecione ao menos um alerta.";
            return RedirectToAction(nameof(Index));
        }

        // O filtro multi-tenant garante que só alertas da própria prefeitura sejam afetados.
        var alertas = await _db.ContratoAlertas.Where(a => ids.Contains(a.Id) && !a.Lido).ToListAsync(ct);
        foreach (var a in alertas) a.Lido = true;
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = $"{alertas.Count} alerta(s) marcado(s) como lido(s).";
        return RedirectToAction(nameof(Index));
    }
}

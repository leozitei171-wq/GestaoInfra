using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Infra.Data;
using SistemaGestaoInfra.Web.Models;

namespace SistemaGestaoInfra.Web.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Contagem por status (o filtro multi-tenant é aplicado automaticamente).
        var porStatus = await _db.StatusAtividades
            .AsNoTracking()
            .Where(s => s.Ativo)
            .OrderBy(s => s.Id)
            .Select(s => new DashboardViewModel.ContagemStatus(
                s.Codigo,
                s.Descricao,
                s.Cor,
                _db.Atividades.Count(a => a.StatusId == s.Id)))
            .ToListAsync(ct);

        int Total(params string[] codigos) =>
            porStatus.Where(s => codigos.Contains(s.Codigo)).Sum(s => s.Total);

        var recentes = await _db.Atividades
            .AsNoTracking()
            .OrderByDescending(a => a.CriadoEm)
            .Take(8)
            .Select(a => new DashboardViewModel.AtividadeRecente(
                a.Id,
                a.Descricao,
                a.TipoAtividade!.Codigo,
                a.Status!.Descricao,
                a.Status.Cor,
                a.CriadoEm))
            .ToListAsync(ct);

        var model = new DashboardViewModel
        {
            TotalAtividades = porStatus.Sum(s => s.Total),
            Abertas = Total("ABERTA"),
            EmAndamento = Total("PROG", "EM_EXEC"),
            Atrasadas = Total("ATRASADA"),
            Concluidas = Total("CONCL"),
            PorStatus = porStatus,
            Recentes = recentes
        };

        ViewData["Title"] = "Dashboard";
        return View(model);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Common;
using SistemaGestaoInfra.Infra.Data;

namespace SistemaGestaoInfra.Web.Controllers;

public record ProgramacaoItem(
    int Id, string Descricao, string Tipo, string? Setor, string Status, string CorStatus,
    string StatusCodigo, string Prioridade, int Progresso,
    DateOnly? Inicio, DateOnly Fim, int DiasParaFim);

public class ProgramacaoViewModel
{
    public string Periodo { get; set; } = "semana";
    public DateOnly Referencia { get; set; }
    public DateOnly De { get; set; }
    public DateOnly Ate { get; set; }
    public int? StatusId { get; set; }
    public IReadOnlyList<ProgramacaoItem> Itens { get; set; } = [];
    public IEnumerable<SelectListItem> Status { get; set; } = [];
}

/// <summary>Atividades com previsão dentro de um período (dia, semana ou mês).</summary>
public class ProgramacaoController : Controller
{
    private readonly AppDbContext _db;

    public ProgramacaoController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? periodo, DateOnly? referencia, int? statusId, bool todos, CancellationToken ct)
    {
        periodo = periodo is "dia" or "semana" or "mes" ? periodo : "semana";
        var hoje = DateOnly.FromDateTime(DateTime.Now);
        var @ref = referencia ?? hoje;
        var (de, ate) = Intervalo(periodo, @ref);

        var statusLista = await _db.StatusAtividades.AsNoTracking().OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.Descricao, s.Codigo }).ToListAsync(ct);

        // Padrão (primeira visita): somente "Programada", como no sistema de referência.
        // "todos=true" vem do botão "Todos os status" e distingue de um filtro nunca definido.
        if (statusId is null && !todos && Request.Query["statusId"].Count == 0)
        {
            statusId = statusLista.FirstOrDefault(s => s.Codigo == StatusCodigos.Programada)?.Id;
        }

        var query = _db.Atividades.AsNoTracking()
            .Where(a => a.DataFimPrevisto != null)
            .Where(a => a.DataFimPrevisto >= de && (a.DataInicioPrevisto ?? a.DataFimPrevisto) <= ate);

        if (statusId.HasValue)
        {
            query = query.Where(a => a.StatusId == statusId);
        }

        var bruto = await query
            .OrderBy(a => a.DataFimPrevisto).ThenBy(a => a.Id)
            .Take(500)
            .Select(a => new
            {
                a.Id, a.Descricao,
                Tipo = a.TipoAtividade!.Codigo,
                Setor = a.Setor != null ? a.Setor.Codigo : null,
                Status = a.Status!.Descricao, a.Status.Cor, Codigo = a.Status.Codigo,
                Prioridade = a.Prioridade!.Codigo,
                a.Progresso, a.DataInicioPrevisto, Fim = a.DataFimPrevisto!.Value
            })
            .ToListAsync(ct);

        var itens = bruto.Select(a => new ProgramacaoItem(
            a.Id, a.Descricao, a.Tipo, a.Setor, a.Status, a.Cor, a.Codigo, a.Prioridade, a.Progresso,
            a.DataInicioPrevisto, a.Fim, a.Fim.DayNumber - hoje.DayNumber)).ToList();

        ViewData["Title"] = "Programação";
        return View(new ProgramacaoViewModel
        {
            Periodo = periodo,
            Referencia = @ref,
            De = de,
            Ate = ate,
            StatusId = statusId,
            Itens = itens,
            Status = statusLista.Select(s => new SelectListItem(s.Descricao, s.Id.ToString(), s.Id == statusId)).ToList()
        });
    }

    private static (DateOnly De, DateOnly Ate) Intervalo(string periodo, DateOnly referencia)
    {
        switch (periodo)
        {
            case "dia":
                return (referencia, referencia);
            case "mes":
                var primeiro = new DateOnly(referencia.Year, referencia.Month, 1);
                return (primeiro, primeiro.AddMonths(1).AddDays(-1));
            default:
                // Semana de segunda a domingo.
                var dow = (int)referencia.DayOfWeek;
                var deslocamento = dow == 0 ? 6 : dow - 1;
                var segunda = referencia.AddDays(-deslocamento);
                return (segunda, segunda.AddDays(6));
        }
    }
}

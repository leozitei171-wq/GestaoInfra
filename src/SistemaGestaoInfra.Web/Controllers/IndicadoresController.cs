using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Common;
using SistemaGestaoInfra.Infra.Data;

namespace SistemaGestaoInfra.Web.Controllers;

public record PontoGrafico(string Rotulo, int Valor);

public class IndicadoresViewModel
{
    public int DiasPeriodo { get; set; } = 30;
    public int Total { get; set; }
    public int Abertas { get; set; }
    public int EmAndamento { get; set; }
    public int Atrasadas { get; set; }
    public int Concluidas { get; set; }

    /// <summary>Percentual de concluídas entre as que já tinham prazo (não negadas).</summary>
    public double TaxaConclusao { get; set; }

    /// <summary>Média de dias entre a abertura e a conclusão. Nulo quando não há concluídas.</summary>
    public double? TempoMedioDias { get; set; }

    public IReadOnlyList<PontoGrafico> PorSetor { get; set; } = [];
    public IReadOnlyList<PontoGrafico> PorTipo { get; set; } = [];
    public IReadOnlyList<PontoGrafico> PorStatus { get; set; } = [];
    public IReadOnlyList<string> CoresStatus { get; set; } = [];
    public IReadOnlyList<PontoGrafico> AbertasPorDia { get; set; } = [];
    public IReadOnlyList<PontoGrafico> ConcluidasPorDia { get; set; } = [];
}

/// <summary>Painel de indicadores de gestão. Restrito a Gestor e Admin.</summary>
[Authorize(Policy = Politicas.GestaoCadastros)]
public class IndicadoresController : Controller
{
    private static readonly int[] PeriodosPermitidos = [7, 30, 90, 365];

    private readonly AppDbContext _db;

    public IndicadoresController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int dias = 30, CancellationToken ct = default)
    {
        if (!PeriodosPermitidos.Contains(dias))
        {
            dias = 30;
        }

        var inicio = DateTime.UtcNow.Date.AddDays(-(dias - 1));
        var noPeriodo = _db.Atividades.AsNoTracking().Where(a => a.CriadoEm >= inicio);

        // Agrupa pelas chaves estrangeiras (traduz para SQL) e resolve os nomes em memória:
        // o EF não traduz agrupamentos por propriedades de relacionamentos opcionais.
        var statusInfo = await _db.StatusAtividades.AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => new { s.Codigo, s.Descricao, s.Cor }, ct);
        var setorInfo = await _db.Setores.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Codigo, ct);
        var tipoInfo = await _db.TiposAtividade.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Codigo, ct);

        var porStatusDb = (await noPeriodo
                .GroupBy(a => a.StatusId)
                .Select(g => new { StatusId = g.Key, Total = g.Count() })
                .ToListAsync(ct))
            .Where(g => statusInfo.ContainsKey(g.StatusId))
            .Select(g => new { statusInfo[g.StatusId].Codigo, statusInfo[g.StatusId].Descricao, statusInfo[g.StatusId].Cor, g.Total })
            .ToList();

        int Soma(params string[] codigos) => porStatusDb.Where(s => codigos.Contains(s.Codigo)).Sum(s => s.Total);

        var total = porStatusDb.Sum(s => s.Total);
        var concluidas = Soma(StatusCodigos.Concluida);
        var negadas = Soma(StatusCodigos.Negada);
        var validas = total - negadas;

        var porSetor = (await noPeriodo
                .GroupBy(a => a.SetorId)
                .Select(g => new { SetorId = g.Key, Total = g.Count() })
                .ToListAsync(ct))
            .Select(g => new PontoGrafico(
                g.SetorId is int sid && setorInfo.TryGetValue(sid, out var cod) ? cod : "Sem setor", g.Total))
            .GroupBy(p => p.Rotulo)
            .Select(g => new PontoGrafico(g.Key, g.Sum(p => p.Valor)))
            .OrderByDescending(p => p.Valor).Take(10)
            .ToList();

        var porTipo = (await noPeriodo
                .GroupBy(a => a.TipoAtividadeId)
                .Select(g => new { TipoId = g.Key, Total = g.Count() })
                .ToListAsync(ct))
            .Select(g => new PontoGrafico(tipoInfo.TryGetValue(g.TipoId, out var cod) ? cod : "?", g.Total))
            .OrderByDescending(p => p.Valor).Take(10)
            .ToList();

        // Tempo de execução: da abertura à conclusão, só das concluídas no período.
        var duracoes = await noPeriodo
            .Where(a => a.DataConclusao != null)
            .Select(a => new { a.CriadoEm, Fim = a.DataConclusao!.Value })
            .ToListAsync(ct);
        double? tempoMedio = duracoes.Count == 0
            ? null
            : Math.Round(duracoes.Average(d => (d.Fim - d.CriadoEm).TotalDays), 1);

        var abertasDia = await noPeriodo
            .GroupBy(a => a.CriadoEm.Date)
            .Select(g => new DiaTotal(g.Key, g.Count()))
            .ToListAsync(ct);

        var concluidasDia = await _db.Atividades.AsNoTracking()
            .Where(a => a.DataConclusao != null && a.DataConclusao >= inicio)
            .GroupBy(a => a.DataConclusao!.Value.Date)
            .Select(g => new DiaTotal(g.Key, g.Count()))
            .ToListAsync(ct);

        // Série contínua (dias sem movimento aparecem com 0).
        var dias_ = Enumerable.Range(0, dias).Select(i => inicio.AddDays(i)).ToList();
        static PontoGrafico Ponto(DateTime dia, List<DiaTotal> origem) =>
            new(dia.ToString("dd/MM"), origem.Where(x => x.Dia.Date == dia.Date).Sum(x => x.Total));

        ViewData["Title"] = "Indicadores";
        return View(new IndicadoresViewModel
        {
            DiasPeriodo = dias,
            Total = total,
            Abertas = Soma(StatusCodigos.Aberta),
            EmAndamento = Soma(StatusCodigos.Programada, StatusCodigos.EmExecucao),
            Atrasadas = Soma(StatusCodigos.Atrasada),
            Concluidas = concluidas,
            TaxaConclusao = validas == 0 ? 0 : Math.Round(concluidas * 100.0 / validas, 1),
            TempoMedioDias = tempoMedio,
            PorSetor = porSetor,
            PorTipo = porTipo,
            PorStatus = porStatusDb.OrderByDescending(s => s.Total).Select(s => new PontoGrafico(s.Descricao, s.Total)).ToList(),
            CoresStatus = porStatusDb.OrderByDescending(s => s.Total).Select(s => s.Cor).ToList(),
            AbertasPorDia = dias_.Select(d => Ponto(d, abertasDia)).ToList(),
            ConcluidasPorDia = dias_.Select(d => Ponto(d, concluidasDia)).ToList()
        });
    }

    private sealed record DiaTotal(DateTime Dia, int Total);
}

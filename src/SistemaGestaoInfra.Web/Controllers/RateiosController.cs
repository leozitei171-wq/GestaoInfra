using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Common;
using SistemaGestaoInfra.Domain.Entities;
using SistemaGestaoInfra.Domain.Enums;
using SistemaGestaoInfra.Infra.Data;
using SistemaGestaoInfra.Web.Services;

namespace SistemaGestaoInfra.Web.Controllers;

public record RateioLista(int Id, DateOnly Data, string Descricao, string? TipoCusto, ModoRateio Modo, decimal ValorTotal, int Itens);

public class RateioFormViewModel
{
    [ModelBinder(typeof(DecimalFlexivelModelBinder))]
    [Display(Name = "Valor total a ratear (R$)")]
    public decimal? ValorTotal { get; set; }

    [Required(ErrorMessage = "Informe a descrição ou o motivo.")]
    [StringLength(255)]
    [Display(Name = "Descrição / motivo")]
    public string Descricao { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a data do rateio.")]
    [Display(Name = "Data do rateio")]
    public DateOnly? DataRateio { get; set; }

    [Display(Name = "Tipo de custo")]
    public int? TipoCustoId { get; set; }

    public ModoRateio Modo { get; set; } = ModoRateio.PorAtividade;

    /// <summary>Contratos ou atividades marcados (conforme o modo). Os pesos são recalculados no servidor.</summary>
    public List<int> ItemIds { get; set; } = new();

    public IEnumerable<SelectListItem> TiposCusto { get; set; } = [];
    public IEnumerable<SelectListItem> Setores { get; set; } = [];
}

public record RateioItemDetalhe(string Rotulo, string? Detalhe, decimal Base, decimal Percentual, decimal Valor);

public class RateioDetalheViewModel
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public DateOnly Data { get; set; }
    public decimal ValorTotal { get; set; }
    public ModoRateio Modo { get; set; }
    public string? TipoCusto { get; set; }
    public string? CriadoPor { get; set; }
    public DateTime CriadoEm { get; set; }
    public IReadOnlyList<RateioItemDetalhe> Itens { get; set; } = [];
    public decimal SomaItens => Itens.Sum(i => i.Valor);
}

/// <summary>Rateio proporcional de custos entre contratos ou atividades. Gestor e Admin.</summary>
[Authorize(Policy = Politicas.GestaoCadastros)]
public class RateiosController : Controller
{
    private const int TamanhoPagina = 20;

    private readonly AppDbContext _db;

    public RateiosController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int pagina = 1, CancellationToken ct = default)
    {
        if (pagina < 1) pagina = 1;

        var total = await _db.Rateios.CountAsync(ct);
        var itens = await _db.Rateios.AsNoTracking()
            .OrderByDescending(r => r.DataRateio).ThenByDescending(r => r.Id)
            .Skip((pagina - 1) * TamanhoPagina).Take(TamanhoPagina)
            .Select(r => new RateioLista(
                r.Id, r.DataRateio, r.Descricao, r.TipoCusto != null ? r.TipoCusto.Codigo : null,
                r.Modo, r.ValorTotal, r.Itens.Count))
            .ToListAsync(ct);

        ViewData["Title"] = "Rateio proporcional";
        ViewData["Pagina"] = pagina;
        ViewData["TotalPaginas"] = (int)Math.Ceiling(total / (double)TamanhoPagina);
        return View(itens);
    }

    public async Task<IActionResult> Detalhe(int id, CancellationToken ct)
    {
        var r = await _db.Rateios.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.Descricao, x.DataRateio, x.ValorTotal, x.Modo, x.CriadoEm, x.CriadoPorId,
                TipoCusto = x.TipoCusto != null ? x.TipoCusto.Codigo + " — " + x.TipoCusto.Descricao : null
            })
            .FirstOrDefaultAsync(ct);
        if (r is null) return NotFound();

        var linhas = await _db.RateioItens.AsNoTracking()
            .Where(i => i.RateioId == id)
            .OrderByDescending(i => i.Valor).ThenBy(i => i.Id)
            .Select(i => new
            {
                Contrato = i.Contrato != null ? i.Contrato.Codigo : null,
                ContratoDesc = i.Contrato != null ? i.Contrato.Descricao : null,
                AtividadeId = i.AtividadeId,
                AtividadeDesc = i.Atividade != null ? i.Atividade.Descricao : null,
                i.Base, i.Percentual, i.Valor
            })
            .ToListAsync(ct);

        var criadoPor = r.CriadoPorId is null ? null
            : await _db.Users.AsNoTracking().Where(u => u.Id == r.CriadoPorId).Select(u => u.NomeCompleto).FirstOrDefaultAsync(ct);

        ViewData["Title"] = $"Rateio #{r.Id}";
        return View(new RateioDetalheViewModel
        {
            Id = r.Id, Descricao = r.Descricao, Data = r.DataRateio, ValorTotal = r.ValorTotal, Modo = r.Modo,
            TipoCusto = r.TipoCusto, CriadoPor = criadoPor, CriadoEm = r.CriadoEm,
            Itens = linhas.Select(l => new RateioItemDetalhe(
                l.Contrato is not null ? l.Contrato : $"Atividade #{l.AtividadeId}",
                l.Contrato is not null ? l.ContratoDesc : l.AtividadeDesc,
                l.Base, l.Percentual, l.Valor)).ToList()
        });
    }

    /// <summary>
    /// Itens elegíveis para o rateio nos setores escolhidos. Setor 0 = itens sem setor.
    /// Contratos pesam pelo valor total; atividades pelo custo dos serviços lançados.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Itens(ModoRateio modo, int[]? setores, CancellationToken ct)
    {
        setores ??= [];
        var incluiSemSetor = setores.Contains(0);
        var ids = setores.Where(s => s > 0).ToArray();

        if (modo == ModoRateio.PorContrato)
        {
            var contratos = await _db.Contratos.AsNoTracking()
                .Where(c => c.Ativo && c.ValorTotal > 0
                         && ((c.SetorId != null && ids.Contains(c.SetorId.Value)) || (incluiSemSetor && c.SetorId == null)))
                .OrderBy(c => c.Codigo)
                .Select(c => new { c.Id, Rotulo = c.Codigo, Detalhe = c.Descricao, Base = c.ValorTotal })
                .ToListAsync(ct);
            var ja = await _db.RateioItens.Where(i => i.ContratoId != null).Select(i => i.ContratoId!.Value).Distinct().ToListAsync(ct);
            return Json(contratos.Select(c => new { c.Id, c.Rotulo, c.Detalhe, c.Base, jaRateado = ja.Contains(c.Id) }));
        }

        var custos = await CustoPorAtividadeAsync(ct);
        var atividades = await _db.Atividades.AsNoTracking()
            .Where(a => (a.SetorId != null && ids.Contains(a.SetorId.Value)) || (incluiSemSetor && a.SetorId == null))
            .OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.Descricao })
            .ToListAsync(ct);
        var jaAtv = await _db.RateioItens.Where(i => i.AtividadeId != null).Select(i => i.AtividadeId!.Value).Distinct().ToListAsync(ct);

        return Json(atividades
            .Where(a => custos.GetValueOrDefault(a.Id) > 0)
            .Select(a => new { a.Id, Rotulo = $"#{a.Id}", Detalhe = a.Descricao, Base = custos[a.Id], jaRateado = jaAtv.Contains(a.Id) }));
    }

    [HttpGet]
    public async Task<IActionResult> Novo(CancellationToken ct)
    {
        var model = new RateioFormViewModel { DataRateio = DateOnly.FromDateTime(DateTime.Now) };
        return await FormularioAsync(model, ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Novo(RateioFormViewModel model, CancellationToken ct)
    {
        if (model.ValorTotal is null or <= 0)
            ModelState.AddModelError(nameof(model.ValorTotal), "O valor total deve ser maior que zero.");

        var ids = model.ItemIds.Distinct().ToList();
        if (ids.Count == 0)
            ModelState.AddModelError(string.Empty, "Selecione ao menos um contrato ou uma atividade.");

        if (model.TipoCustoId is int tc && !await _db.TiposCusto.AnyAsync(t => t.Id == tc && t.Ativo, ct))
            ModelState.AddModelError(nameof(model.TipoCustoId), "Tipo de custo inválido.");

        // Pesos SEMPRE recalculados aqui, a partir do banco: nada do que o navegador mandar vira peso.
        var pesos = new List<(int Id, decimal Base)>();
        if (ids.Count > 0)
        {
            if (model.Modo == ModoRateio.PorContrato)
            {
                var encontrados = await _db.Contratos.AsNoTracking()
                    .Where(c => ids.Contains(c.Id) && c.Ativo).Select(c => new { c.Id, c.ValorTotal }).ToListAsync(ct);
                pesos = encontrados.Select(c => (c.Id, c.ValorTotal)).ToList();
            }
            else
            {
                var custos = await CustoPorAtividadeAsync(ct);
                var existentes = await _db.Atividades.AsNoTracking().Where(a => ids.Contains(a.Id)).Select(a => a.Id).ToListAsync(ct);
                pesos = existentes.Select(id => (id, custos.GetValueOrDefault(id))).ToList();
            }

            if (pesos.Count != ids.Count)
                ModelState.AddModelError(string.Empty, "Há itens selecionados inexistentes ou inativos. Recarregue a lista e tente novamente.");
            else if (pesos.Sum(p => p.Base) <= 0)
                ModelState.AddModelError(string.Empty, "Os itens selecionados não têm valor-base para ratear (valor do contrato ou custo da atividade igual a zero).");
        }

        if (!ModelState.IsValid) return await FormularioAsync(model, ct);

        pesos = pesos.OrderBy(p => p.Id).ToList();
        var valores = CalculoRateio.Dividir(model.ValorTotal!.Value, pesos.Select(p => p.Base).ToList());
        var somaBase = pesos.Sum(p => p.Base);

        var rateio = new Rateio
        {
            Descricao = model.Descricao.Trim(),
            ValorTotal = Math.Round(model.ValorTotal.Value, 2),
            DataRateio = model.DataRateio!.Value,
            Modo = model.Modo,
            TipoCustoId = model.TipoCustoId,
            CriadoPorId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        };

        for (var i = 0; i < pesos.Count; i++)
        {
            rateio.Itens.Add(new RateioItem
            {
                ContratoId = model.Modo == ModoRateio.PorContrato ? pesos[i].Id : null,
                AtividadeId = model.Modo == ModoRateio.PorAtividade ? pesos[i].Id : null,
                Base = pesos[i].Base,
                Percentual = Math.Round(pesos[i].Base * 100 / somaBase, 4),
                Valor = valores[i]
            });
        }

        _db.Rateios.Add(rateio);
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = $"Rateio de {rateio.ValorTotal:C2} distribuído entre {pesos.Count} item(ns).";
        return RedirectToAction(nameof(Detalhe), new { id = rateio.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Excluir(int id, CancellationToken ct)
    {
        var r = await _db.Rateios.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return NotFound();

        _db.Rateios.Remove(r);   // os itens saem em cascata; a exclusão fica registrada na auditoria
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = $"Rateio #{id} excluído.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<Dictionary<int, decimal>> CustoPorAtividadeAsync(CancellationToken ct)
    {
        var linhas = await _db.AtividadeServicos.AsNoTracking()
            .Where(s => s.Atividade!.Status!.Codigo != StatusCodigos.Negada)
            .GroupBy(s => s.AtividadeId)
            .Select(g => new { AtividadeId = g.Key, Custo = g.Sum(x => x.Quantidade * x.ValorUnitario) })
            .ToListAsync(ct);
        return linhas.ToDictionary(l => l.AtividadeId, l => l.Custo);
    }

    private async Task<IActionResult> FormularioAsync(RateioFormViewModel model, CancellationToken ct)
    {
        model.TiposCusto = await _db.TiposCusto.AsNoTracking().Where(t => t.Ativo).OrderBy(t => t.Codigo)
            .Select(t => new SelectListItem(t.Codigo + " — " + t.Descricao, t.Id.ToString())).ToListAsync(ct);
        var setores = await _db.Setores.AsNoTracking().Where(s => s.Ativo).OrderBy(s => s.Codigo)
            .Select(s => new SelectListItem(s.Codigo + " — " + s.Descricao, s.Id.ToString())).ToListAsync(ct);
        setores.Add(new SelectListItem("(itens sem setor)", "0"));
        model.Setores = setores;

        ViewData["Title"] = "Novo rateio proporcional";
        return View("Novo", model);
    }
}

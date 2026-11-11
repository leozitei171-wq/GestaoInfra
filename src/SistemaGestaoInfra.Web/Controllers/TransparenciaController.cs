using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Common;
using SistemaGestaoInfra.Infra.Data;

namespace SistemaGestaoInfra.Web.Controllers;

public record TransparenciaLinha(
    int Id, string Descricao, string Tipo, string? Setor, string? Local,
    string Situacao, string Cor, int Progresso, DateTime Registro);

public class TransparenciaViewModel
{
    public IReadOnlyList<SelectListItem> Prefeituras { get; set; } = [];
    public int? PrefeituraId { get; set; }
    public string? PrefeituraNome { get; set; }
    public string? Bairro { get; set; }
    public int? StatusId { get; set; }
    public IReadOnlyList<SelectListItem> Bairros { get; set; } = [];
    public IReadOnlyList<SelectListItem> Status { get; set; } = [];
    public IReadOnlyList<TransparenciaLinha> Itens { get; set; } = [];
    public int Total { get; set; }
    public int Pagina { get; set; } = 1;
    public int TotalPaginas => (int)Math.Ceiling(Total / (double)TransparenciaController.TamanhoPagina);
}

public class TransparenciaDetalheViewModel
{
    public int PrefeituraId { get; set; }
    public string PrefeituraNome { get; set; } = string.Empty;
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Setor { get; set; }
    public string? Local { get; set; }
    public string Situacao { get; set; } = string.Empty;
    public string Cor { get; set; } = string.Empty;
    public string Prioridade { get; set; } = string.Empty;
    public int Progresso { get; set; }
    public DateOnly? InicioPrevisto { get; set; }
    public DateOnly? FimPrevisto { get; set; }
    public DateTime Registro { get; set; }
}

/// <summary>
/// Consulta pública (transparência): acessível SEM login. Expõe apenas dados não pessoais:
/// nunca mostra quem abriu, foto, requerimento, rua/número nem coordenadas — só o bairro.
/// Textos livres passam pelo <see cref="Anonimizador"/>.
/// </summary>
/// <remarks>
/// Sem usuário autenticado o filtro multi-tenant do DbContext não retorna nada, por isso todas as
/// consultas usam IgnoreQueryFilters com um filtro EXPLÍCITO de prefeitura. Qualquer nova consulta
/// aqui precisa repetir esse filtro, senão exporia dados de outras prefeituras.
/// </remarks>
[AllowAnonymous]
[EnableRateLimiting(PoliticaPublica)]
public class TransparenciaController : Controller
{
    public const string PoliticaPublica = "publico";
    public const int TamanhoPagina = 20;

    private readonly AppDbContext _db;

    public TransparenciaController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int? prefeitura, string? bairro, int? statusId, int pagina = 1, CancellationToken ct = default)
    {
        if (pagina < 1) pagina = 1;

        var prefeituras = await _db.Prefeituras.AsNoTracking()
            .Where(p => p.Ativo).OrderBy(p => p.Nome)
            .Select(p => new { p.Id, p.Nome, p.Cidade, p.Uf }).ToListAsync(ct);

        // Com uma única prefeitura ativa, entra direto nela.
        var escolhida = prefeitura is int pid
            ? prefeituras.FirstOrDefault(p => p.Id == pid)
            : prefeituras.Count == 1 ? prefeituras[0] : null;

        var model = new TransparenciaViewModel
        {
            Prefeituras = prefeituras.Select(p => new SelectListItem($"{p.Nome} — {p.Cidade}/{p.Uf}", p.Id.ToString(), p.Id == escolhida?.Id)).ToList(),
            PrefeituraId = escolhida?.Id,
            PrefeituraNome = escolhida is null ? null : $"{escolhida.Nome} — {escolhida.Cidade}/{escolhida.Uf}",
            Pagina = pagina
        };

        ViewData["Title"] = "Transparência";
        if (escolhida is null) return View(model);   // mostra apenas o seletor de prefeitura

        var tenant = escolhida.Id;
        var query = _db.Atividades.IgnoreQueryFilters().AsNoTracking().Where(a => a.PrefeituraId == tenant);

        model.Bairro = string.IsNullOrWhiteSpace(bairro) ? null : bairro.Trim();
        model.StatusId = statusId;
        if (model.Bairro is not null) query = query.Where(a => a.Bairro == model.Bairro);
        if (statusId.HasValue) query = query.Where(a => a.StatusId == statusId);

        model.Total = await query.CountAsync(ct);

        var bruto = await query
            .OrderByDescending(a => a.CriadoEm).ThenByDescending(a => a.Id)
            .Skip((pagina - 1) * TamanhoPagina).Take(TamanhoPagina)
            .Select(a => new
            {
                a.Id, a.Descricao, Tipo = a.TipoAtividade!.Codigo,
                Setor = a.Setor != null ? a.Setor.Codigo : null,
                a.Bairro, Situacao = a.Status!.Descricao, a.Status.Cor, a.Progresso, Registro = a.CriadoEm
            })
            .ToListAsync(ct);

        model.Itens = bruto.Select(a => new TransparenciaLinha(
            a.Id, Anonimizador.Resumir(a.Descricao, 120), a.Tipo, a.Setor,
            string.IsNullOrWhiteSpace(a.Bairro) ? null : a.Bairro,
            a.Situacao, a.Cor, a.Progresso, a.Registro)).ToList();

        model.Bairros = (await _db.Atividades.IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.PrefeituraId == tenant && a.Bairro != null && a.Bairro != "")
                .Select(a => a.Bairro!).Distinct().OrderBy(b => b).ToListAsync(ct))
            .Select(b => new SelectListItem(b, b, b == model.Bairro)).ToList();

        model.Status = (await _db.StatusAtividades.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.PrefeituraId == tenant && s.Ativo).OrderBy(s => s.Id)
                .Select(s => new { s.Id, s.Descricao }).ToListAsync(ct))
            .Select(s => new SelectListItem(s.Descricao, s.Id.ToString(), s.Id == statusId)).ToList();

        return View(model);
    }

    public async Task<IActionResult> Ver(int id, int prefeitura, CancellationToken ct)
    {
        var p = await _db.Prefeituras.AsNoTracking()
            .Where(x => x.Id == prefeitura && x.Ativo)
            .Select(x => new { x.Id, x.Nome, x.Cidade, x.Uf }).FirstOrDefaultAsync(ct);
        if (p is null) return NotFound();

        // O id da atividade só vale dentro da prefeitura informada: id de outra prefeitura => 404.
        var a = await _db.Atividades.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == id && x.PrefeituraId == p.Id)
            .Select(x => new
            {
                x.Id, x.Descricao, Tipo = x.TipoAtividade!.Codigo + " — " + x.TipoAtividade.Descricao,
                Setor = x.Setor != null ? x.Setor.Codigo : null, x.Bairro,
                Situacao = x.Status!.Descricao, x.Status.Cor, Prioridade = x.Prioridade!.Descricao,
                x.Progresso, x.DataInicioPrevisto, x.DataFimPrevisto, Registro = x.CriadoEm
            })
            .FirstOrDefaultAsync(ct);
        if (a is null) return NotFound();

        ViewData["Title"] = $"Demanda #{a.Id}";
        return View(new TransparenciaDetalheViewModel
        {
            PrefeituraId = p.Id, PrefeituraNome = $"{p.Nome} — {p.Cidade}/{p.Uf}",
            Id = a.Id, Descricao = Anonimizador.Mascarar(a.Descricao), Tipo = a.Tipo, Setor = a.Setor,
            Local = string.IsNullOrWhiteSpace(a.Bairro) ? null : a.Bairro,
            Situacao = a.Situacao, Cor = a.Cor, Prioridade = a.Prioridade, Progresso = a.Progresso,
            InicioPrevisto = a.DataInicioPrevisto, FimPrevisto = a.DataFimPrevisto, Registro = a.Registro
        });
    }
}

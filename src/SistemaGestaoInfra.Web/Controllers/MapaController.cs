using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Infra.Data;

namespace SistemaGestaoInfra.Web.Controllers;

/// <summary>
/// Mapa de atividades: marcadores coloridos por status, com filtros e atualização automática.
/// O isolamento por prefeitura é garantido pelo filtro global do DbContext.
/// </summary>
public class MapaController : Controller
{
    private const int LimiteMarcadores = 2000;

    private readonly AppDbContext _db;

    public MapaController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var status = await _db.StatusAtividades.AsNoTracking()
            .Where(s => s.Ativo).OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.Descricao, s.Cor })
            .ToListAsync(ct);

        ViewData["Title"] = "Mapa de atividades";
        ViewData["Legenda"] = status.Select(s => (s.Descricao, s.Cor)).ToList();
        ViewData["Status"] = status.Select(s => new SelectListItem(s.Descricao, s.Id.ToString())).ToList();

        ViewData["Setores"] = await _db.Setores.AsNoTracking().OrderBy(s => s.Codigo)
            .Select(s => new SelectListItem(s.Codigo, s.Id.ToString())).ToListAsync(ct);
        ViewData["Tipos"] = await _db.TiposAtividade.AsNoTracking().OrderBy(t => t.Codigo)
            .Select(t => new SelectListItem(t.Codigo + " — " + t.Descricao, t.Id.ToString())).ToListAsync(ct);
        ViewData["Prioridades"] = await _db.Prioridades.AsNoTracking().OrderBy(p => p.Ordem)
            .Select(p => new SelectListItem(p.Descricao, p.Id.ToString())).ToListAsync(ct);

        // Bairros vêm das próprias atividades (ainda não há cadastro de bairros).
        ViewData["Bairros"] = await _db.Atividades.AsNoTracking()
            .Where(a => a.Bairro != null && a.Bairro != "")
            .Select(a => a.Bairro!).Distinct().OrderBy(b => b)
            .Select(b => new SelectListItem(b, b)).ToListAsync(ct);

        return View();
    }

    /// <summary>Dados dos marcadores em JSON. Só retorna atividades com coordenadas.</summary>
    [HttpGet]
    public async Task<IActionResult> Dados(
        int? statusId, int? setorId, int? tipoId, int? prioridadeId,
        string? bairro, DateTime? de, DateTime? ate, CancellationToken ct)
    {
        var query = _db.Atividades.AsNoTracking().AsQueryable();

        if (statusId.HasValue) query = query.Where(a => a.StatusId == statusId);
        if (setorId.HasValue) query = query.Where(a => a.SetorId == setorId);
        if (tipoId.HasValue) query = query.Where(a => a.TipoAtividadeId == tipoId);
        if (prioridadeId.HasValue) query = query.Where(a => a.PrioridadeId == prioridadeId);
        if (!string.IsNullOrWhiteSpace(bairro)) query = query.Where(a => a.Bairro == bairro);

        if (de.HasValue)
        {
            var inicio = DateTime.SpecifyKind(de.Value.Date, DateTimeKind.Local).ToUniversalTime();
            query = query.Where(a => a.CriadoEm >= inicio);
        }

        if (ate.HasValue)
        {
            var fim = DateTime.SpecifyKind(ate.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            query = query.Where(a => a.CriadoEm < fim);
        }

        // Atividades que passam nos filtros mas não podem ser desenhadas (sem coordenadas).
        var semLocalizacao = await query.CountAsync(a => a.Latitude == null || a.Longitude == null, ct);

        query = query.Where(a => a.Latitude != null && a.Longitude != null);
        var total = await query.CountAsync(ct);

        var itens = await query
            .OrderByDescending(a => a.CriadoEm)
            .Take(LimiteMarcadores)
            .Select(a => new
            {
                a.Id,
                lat = a.Latitude,
                lng = a.Longitude,
                descricao = a.Descricao,
                tipo = a.TipoAtividade!.Codigo,
                status = a.Status!.Descricao,
                cor = a.Status.Cor,
                prioridade = a.Prioridade!.Descricao,
                rua = a.Rua,
                numero = a.Numero,
                bairro = a.Bairro,
                progresso = a.Progresso,
                criadoEm = a.CriadoEm
            })
            .ToListAsync(ct);

        Response.Headers.CacheControl = "no-store";
        return Json(new
        {
            total,
            semLocalizacao,
            truncado = total > LimiteMarcadores,
            atualizadoEm = DateTime.UtcNow,
            itens
        });
    }
}

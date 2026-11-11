using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Entities;
using SistemaGestaoInfra.Infra.Data;

namespace SistemaGestaoInfra.Web.Controllers;

public class AuditoriaFiltro
{
    public string? Entidade { get; set; }
    public string? Acao { get; set; }
    public string? Usuario { get; set; }
    public DateTime? De { get; set; }
    public DateTime? Ate { get; set; }
    public int Pagina { get; set; } = 1;

    public const int TamanhoPagina = 25;
}

public class AuditoriaViewModel
{
    public AuditoriaFiltro Filtro { get; set; } = new();
    public IReadOnlyList<Auditoria> Itens { get; set; } = [];
    public IEnumerable<SelectListItem> Entidades { get; set; } = [];
    public int Total { get; set; }
    public int TotalPaginas => (int)Math.Ceiling(Total / (double)AuditoriaFiltro.TamanhoPagina);
}

/// <summary>
/// Consulta da trilha de auditoria (somente leitura). Não existe ação de editar ou excluir.
/// Gestor vê apenas a própria prefeitura; admin vê todas (filtro global do DbContext).
/// </summary>
[Authorize(Policy = Politicas.GestaoCadastros)]
public class AuditoriaController : Controller
{
    private readonly AppDbContext _db;

    public AuditoriaController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index([FromQuery] AuditoriaFiltro filtro, CancellationToken ct)
    {
        if (filtro.Pagina < 1)
        {
            filtro.Pagina = 1;
        }

        var query = _db.Auditorias.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Entidade))
        {
            query = query.Where(a => a.Entidade == filtro.Entidade);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Acao))
        {
            query = query.Where(a => EF.Functions.ILike(a.Acao, $"%{EscaparLike(filtro.Acao.Trim())}%"));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Usuario))
        {
            query = query.Where(a => a.UsuarioNome != null
                && EF.Functions.ILike(a.UsuarioNome, $"%{EscaparLike(filtro.Usuario.Trim())}%"));
        }

        if (filtro.De.HasValue)
        {
            var de = DateTime.SpecifyKind(filtro.De.Value.Date, DateTimeKind.Local).ToUniversalTime();
            query = query.Where(a => a.QuandoEm >= de);
        }

        if (filtro.Ate.HasValue)
        {
            var ate = DateTime.SpecifyKind(filtro.Ate.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            query = query.Where(a => a.QuandoEm < ate);
        }

        var total = await query.CountAsync(ct);
        var itens = await query
            .OrderByDescending(a => a.QuandoEm).ThenByDescending(a => a.Id)
            .Skip((filtro.Pagina - 1) * AuditoriaFiltro.TamanhoPagina)
            .Take(AuditoriaFiltro.TamanhoPagina)
            .ToListAsync(ct);

        var entidades = await _db.Auditorias.AsNoTracking()
            .Select(a => a.Entidade).Distinct().OrderBy(e => e)
            .Select(e => new SelectListItem(e, e))
            .ToListAsync(ct);

        ViewData["Title"] = "Auditoria";
        return View(new AuditoriaViewModel { Filtro = filtro, Itens = itens, Total = total, Entidades = entidades });
    }

    /// <summary>Evita que % e _ digitados pelo usuário atuem como curingas do LIKE.</summary>
    private static string EscaparLike(string texto) =>
        texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}

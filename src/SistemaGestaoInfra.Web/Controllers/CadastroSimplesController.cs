using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Common;
using SistemaGestaoInfra.Infra.Data;
using SistemaGestaoInfra.Web.Models;

namespace SistemaGestaoInfra.Web.Controllers;

public class CadastroSimplesViewModel : CadastroBaseViewModel
{
}

/// <summary>
/// CRUD de cadastros do tipo código + descrição + situação. Cada cadastro concreto só
/// informa o conjunto de dados, os títulos e a regra de "em uso".
/// </summary>
[Authorize(Policy = Politicas.GestaoCadastros)]
public abstract class CadastroSimplesController<T> : Controller
    where T : EntidadeTenant, ICadastroSimples, new()
{
    private const string ViewIndex = "~/Views/Shared/CadastroSimples/Index.cshtml";
    private const string ViewForm = "~/Views/Shared/CadastroSimples/Form.cshtml";

    protected readonly AppDbContext Db;

    protected CadastroSimplesController(AppDbContext db)
    {
        Db = db;
    }

    protected abstract DbSet<T> Conjunto { get; }
    protected abstract string TituloPlural { get; }
    protected abstract string TituloSingular { get; }
    protected abstract int TamanhoMaxCodigo { get; }

    /// <summary>Indica se o registro está referenciado e, por isso, não pode ser excluído.</summary>
    protected abstract Task<bool> EmUsoAsync(int id, CancellationToken ct);

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        Titulos();
        ViewData["Title"] = TituloPlural;
        return View(ViewIndex, await Conjunto.AsNoTracking().OrderBy(x => x.Codigo).ToListAsync(ct));
    }

    [HttpGet]
    public IActionResult Novo()
    {
        Titulos();
        ViewData["Title"] = $"Novo(a) {TituloSingular.ToLowerInvariant()}";
        return View(ViewForm, new CadastroSimplesViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Novo(CadastroSimplesViewModel model, CancellationToken ct)
    {
        model.Id = 0;
        await ValidarAsync(model, ct);
        if (!ModelState.IsValid)
        {
            return Formulario(model, $"Novo(a) {TituloSingular.ToLowerInvariant()}");
        }

        Conjunto.Add(new T
        {
            Codigo = model.Codigo.Trim().ToUpperInvariant(),
            Descricao = model.Descricao.Trim(),
            Ativo = model.Ativo
        });
        await Db.SaveChangesAsync(ct);

        TempData["Sucesso"] = $"{TituloSingular} criado(a) com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id, CancellationToken ct)
    {
        var item = await Conjunto.FindAsync([id], ct);
        if (item is null) return NotFound();

        return Formulario(new CadastroSimplesViewModel
        {
            Id = item.Id, Codigo = item.Codigo, Descricao = item.Descricao, Ativo = item.Ativo
        }, $"Editar {TituloSingular.ToLowerInvariant()}");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, CadastroSimplesViewModel model, CancellationToken ct)
    {
        if (id != model.Id) return BadRequest();

        var item = await Conjunto.FindAsync([id], ct);
        if (item is null) return NotFound();

        await ValidarAsync(model, ct);
        if (!ModelState.IsValid)
        {
            return Formulario(model, $"Editar {TituloSingular.ToLowerInvariant()}");
        }

        item.Codigo = model.Codigo.Trim().ToUpperInvariant();
        item.Descricao = model.Descricao.Trim();
        item.Ativo = model.Ativo;
        await Db.SaveChangesAsync(ct);

        TempData["Sucesso"] = $"{TituloSingular} atualizado(a).";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Excluir(int id, CancellationToken ct)
    {
        var item = await Conjunto.FindAsync([id], ct);
        if (item is null) return NotFound();

        if (await EmUsoAsync(id, ct))
        {
            TempData["Erro"] = $"\"{item.Codigo}\" está em uso e não pode ser excluído(a). Desative-o(a) em vez de excluir.";
            return RedirectToAction(nameof(Index));
        }

        Conjunto.Remove(item);
        await Db.SaveChangesAsync(ct);

        TempData["Sucesso"] = $"\"{item.Codigo}\" excluído(a).";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult Formulario(CadastroSimplesViewModel model, string titulo)
    {
        Titulos();
        ViewData["Title"] = titulo;
        return View(ViewForm, model);
    }

    private void Titulos()
    {
        ViewData["TituloSingular"] = TituloSingular;
        ViewData["TamanhoMaxCodigo"] = TamanhoMaxCodigo;
    }

    private async Task ValidarAsync(CadastroSimplesViewModel model, CancellationToken ct)
    {
        var codigo = model.Codigo?.Trim().ToUpperInvariant() ?? string.Empty;
        if (codigo.Length == 0) return;

        if (codigo.Length > TamanhoMaxCodigo)
        {
            ModelState.AddModelError(nameof(model.Codigo), $"O código deve ter no máximo {TamanhoMaxCodigo} caracteres.");
            return;
        }

        if (await Conjunto.AnyAsync(x => x.Codigo.ToUpper() == codigo && x.Id != model.Id, ct))
        {
            ModelState.AddModelError(nameof(model.Codigo), "Já existe um registro com este código.");
        }
    }
}

public class TiposMedicaoController : CadastroSimplesController<Domain.Entities.TipoMedicao>
{
    public TiposMedicaoController(AppDbContext db) : base(db) { }

    protected override DbSet<Domain.Entities.TipoMedicao> Conjunto => Db.TiposMedicao;
    protected override string TituloPlural => "Tipos de medição";
    protected override string TituloSingular => "Tipo de medição";
    protected override int TamanhoMaxCodigo => 20;

    protected override Task<bool> EmUsoAsync(int id, CancellationToken ct) =>
        Db.ContratoServicos.AnyAsync(i => i.TipoMedicaoId == id, ct);
}

public class TiposCustoController : CadastroSimplesController<Domain.Entities.TipoCusto>
{
    public TiposCustoController(AppDbContext db) : base(db) { }

    protected override DbSet<Domain.Entities.TipoCusto> Conjunto => Db.TiposCusto;
    protected override string TituloPlural => "Tipos de custo";
    protected override string TituloSingular => "Tipo de custo";
    protected override int TamanhoMaxCodigo => 30;

    protected override Task<bool> EmUsoAsync(int id, CancellationToken ct) =>
        Db.Rateios.AnyAsync(r => r.TipoCustoId == id, ct);
}

public class TiposServicoController : CadastroSimplesController<Domain.Entities.TipoServico>
{
    public TiposServicoController(AppDbContext db) : base(db) { }

    protected override DbSet<Domain.Entities.TipoServico> Conjunto => Db.TiposServico;
    protected override string TituloPlural => "Tipos de serviço";
    protected override string TituloSingular => "Tipo de serviço";
    protected override int TamanhoMaxCodigo => 40;

    protected override Task<bool> EmUsoAsync(int id, CancellationToken ct) =>
        Db.ContratoServicos.AnyAsync(i => i.TipoServicoId == id, ct);
}

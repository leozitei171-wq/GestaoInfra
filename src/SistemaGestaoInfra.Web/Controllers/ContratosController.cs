using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Application.Common.Interfaces;
using SistemaGestaoInfra.Domain.Entities;
using SistemaGestaoInfra.Infra.Data;

namespace SistemaGestaoInfra.Web.Controllers;

/// <summary>Contratos com fornecedores: vigência, serviços contratados, valor e PDF.</summary>
[Authorize(Policy = Politicas.GestaoCadastros)]
public partial class ContratosController : Controller
{
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;

    public ContratosController(AppDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var consumo = await ConsumoContratos.ValorPorContratoAsync(_db, ct);

        var contratos = await _db.Contratos.AsNoTracking()
            .OrderByDescending(c => c.DataFim)
            .Select(c => new
            {
                c.Id, c.Codigo, c.Descricao,
                Fornecedor = c.Fornecedor!.RazaoSocial,
                c.DataInicio, c.DataFim, c.ValorTotal, c.Ativo,
                TemPdf = c.ArquivoPdfPath != null
            })
            .ToListAsync(ct);

        ViewData["Title"] = "Contratos";
        return View(contratos.Select(c => new ContratoLista(
            c.Id, c.Codigo, c.Descricao, c.Fornecedor, c.DataInicio, c.DataFim, c.ValorTotal,
            consumo.GetValueOrDefault(c.Id), c.Ativo, c.TemPdf)).ToList());
    }

    public async Task<IActionResult> Detalhe(int id, CancellationToken ct)
    {
        var c = await _db.Contratos.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.Codigo, x.Descricao, Fornecedor = x.Fornecedor!.RazaoSocial,
                Setor = x.Setor != null ? x.Setor.Codigo : null,
                x.DataInicio, x.DataFim, x.ValorTotal, x.Ativo, TemPdf = x.ArquivoPdfPath != null
            })
            .FirstOrDefaultAsync(ct);
        if (c is null) return NotFound();

        var itens = await _db.ContratoServicos.AsNoTracking()
            .Where(i => i.ContratoId == id)
            .OrderBy(i => i.TipoServico!.Codigo)
            .Select(i => new
            {
                i.Id, Servico = i.TipoServico!.Codigo + " — " + i.TipoServico.Descricao,
                Medicao = i.TipoMedicao!.Codigo,
                i.Quantidade, i.ValorUnitario, i.Limite
            })
            .ToListAsync(ct);

        var executado = await ConsumoContratos.QuantidadePorItemAsync(_db, id, ct);
        var consumido = (await ConsumoContratos.ValorPorContratoAsync(_db, ct)).GetValueOrDefault(id);

        ViewData["Title"] = $"Contrato {c.Codigo}";
        return View(new ContratoDetalheViewModel
        {
            Id = c.Id, Codigo = c.Codigo, Descricao = c.Descricao, Fornecedor = c.Fornecedor, Setor = c.Setor,
            DataInicio = c.DataInicio, DataFim = c.DataFim, ValorTotal = c.ValorTotal, Ativo = c.Ativo,
            TemPdf = c.TemPdf, Consumido = consumido,
            Itens = itens.Select(i => new ContratoItemDetalhe(
                i.Servico, i.Medicao, i.Quantidade, i.ValorUnitario, i.Limite, executado.GetValueOrDefault(i.Id))).ToList()
        });
    }

    /// <summary>Entrega o PDF do contrato (apenas Gestor/Admin, restrito à prefeitura do usuário).</summary>
    [HttpGet]
    public async Task<IActionResult> Pdf(int id, CancellationToken ct)
    {
        var caminho = await _db.Contratos.AsNoTracking()
            .Where(c => c.Id == id).Select(c => c.ArquivoPdfPath).FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(caminho)) return NotFound();

        var stream = await _storage.AbrirAsync(caminho, ct);
        if (stream is null) return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "private, no-store";
        return File(stream, "application/pdf");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Excluir(int id, CancellationToken ct)
    {
        var c = await _db.Contratos.Include(x => x.Servicos).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();

        var itensIds = c.Servicos.Select(s => s.Id).ToList();
        if (await _db.AtividadeServicos.AnyAsync(a => itensIds.Contains(a.ContratoServicoId), ct))
        {
            TempData["Erro"] = $"O contrato \"{c.Codigo}\" já foi usado em atividades e não pode ser excluído. Desative-o em vez de excluir.";
            return RedirectToAction(nameof(Index));
        }

        var pdf = c.ArquivoPdfPath;
        _db.Contratos.Remove(c);
        await _db.SaveChangesAsync(ct);
        if (pdf is not null) await _storage.RemoverAsync(pdf, ct);

        TempData["Sucesso"] = $"Contrato \"{c.Codigo}\" excluído.";
        return RedirectToAction(nameof(Index));
    }
}

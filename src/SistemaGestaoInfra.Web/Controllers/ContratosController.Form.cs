using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Entities;
using SistemaGestaoInfra.Infra.Data;
using SistemaGestaoInfra.Web.Services;

namespace SistemaGestaoInfra.Web.Controllers;

public partial class ContratosController
{
    [HttpGet]
    public async Task<IActionResult> Novo(CancellationToken ct)
    {
        var model = new ContratoFormViewModel { Itens = { new ContratoItemViewModel() } };
        return await FormularioAsync(model, "Novo contrato", ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Novo(ContratoFormViewModel model, CancellationToken ct)
    {
        model.Id = 0;
        await ValidarAsync(model, null, ct);
        if (!ModelState.IsValid) return await FormularioAsync(model, "Novo contrato", ct);

        string? pdf = null;
        try
        {
            pdf = await SalvarPdfAsync(model.ArquivoPdf, ct);

            var contrato = new Contrato { ArquivoPdfPath = pdf };
            Aplicar(contrato, model);
            foreach (var item in model.Itens)
            {
                contrato.Servicos.Add(NovoItem(item));
            }

            contrato.ValorTotal = TotalDe(contrato.Servicos);
            _db.Contratos.Add(contrato);
            await _db.SaveChangesAsync(ct);

            TempData["Sucesso"] = $"Contrato \"{contrato.Codigo}\" criado — valor total {contrato.ValorTotal:C2}.";
            return RedirectToAction(nameof(Detalhe), new { id = contrato.Id });
        }
        catch
        {
            if (pdf is not null) await _storage.RemoverAsync(pdf, CancellationToken.None);
            throw;
        }
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id, CancellationToken ct)
    {
        var c = await _db.Contratos.AsNoTracking().Include(x => x.Servicos).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();

        var model = new ContratoFormViewModel
        {
            Id = c.Id, Codigo = c.Codigo, Descricao = c.Descricao, FornecedorId = c.FornecedorId, SetorId = c.SetorId,
            DataInicio = c.DataInicio, DataFim = c.DataFim, TemPdf = c.ArquivoPdfPath != null, Ativo = c.Ativo,
            Itens = c.Servicos.OrderBy(s => s.Id).Select(s => new ContratoItemViewModel
            {
                Id = s.Id, TipoServicoId = s.TipoServicoId, TipoMedicaoId = s.TipoMedicaoId,
                Quantidade = s.Quantidade, ValorUnitario = s.ValorUnitario, Limite = s.Limite
            }).ToList()
        };
        return await FormularioAsync(model, $"Editar contrato {c.Codigo}", ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Editar(int id, ContratoFormViewModel model, CancellationToken ct)
    {
        if (id != model.Id) return BadRequest();

        var contrato = await _db.Contratos.Include(x => x.Servicos).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (contrato is null) return NotFound();

        await ValidarAsync(model, contrato, ct);
        if (!ModelState.IsValid)
        {
            model.TemPdf = contrato.ArquivoPdfPath != null;
            return await FormularioAsync(model, $"Editar contrato {contrato.Codigo}", ct);
        }

        string? novoPdf = null;
        try
        {
            novoPdf = await SalvarPdfAsync(model.ArquivoPdf, ct);
            var pdfAntigo = contrato.ArquivoPdfPath;

            Aplicar(contrato, model);
            if (novoPdf is not null) contrato.ArquivoPdfPath = novoPdf;

            // Sincroniza os itens preservando a identidade dos existentes (atividades apontam para eles).
            var mantidos = model.Itens.Where(i => i.Id > 0).ToDictionary(i => i.Id);
            foreach (var existente in contrato.Servicos.ToList())
            {
                if (mantidos.TryGetValue(existente.Id, out var vm))
                {
                    existente.TipoServicoId = vm.TipoServicoId!.Value;
                    existente.TipoMedicaoId = vm.TipoMedicaoId!.Value;
                    existente.Quantidade = vm.Quantidade!.Value;
                    existente.ValorUnitario = vm.ValorUnitario!.Value;
                    existente.Limite = vm.Limite;
                }
                else
                {
                    _db.ContratoServicos.Remove(existente);
                    contrato.Servicos.Remove(existente);
                }
            }

            foreach (var novo in model.Itens.Where(i => i.Id == 0))
            {
                contrato.Servicos.Add(NovoItem(novo));
            }

            contrato.ValorTotal = TotalDe(contrato.Servicos);
            await _db.SaveChangesAsync(ct);

            if (novoPdf is not null && pdfAntigo is not null) await _storage.RemoverAsync(pdfAntigo, ct);

            TempData["Sucesso"] = $"Contrato \"{contrato.Codigo}\" atualizado — valor total {contrato.ValorTotal:C2}.";
            return RedirectToAction(nameof(Detalhe), new { id });
        }
        catch
        {
            if (novoPdf is not null) await _storage.RemoverAsync(novoPdf, CancellationToken.None);
            throw;
        }
    }

    // ---------------------------------------------------------------- helpers

    private static void Aplicar(Contrato c, ContratoFormViewModel m)
    {
        c.Codigo = m.Codigo.Trim().ToUpperInvariant();
        c.Descricao = m.Descricao.Trim();
        c.FornecedorId = m.FornecedorId!.Value;
        c.SetorId = m.SetorId;
        c.DataInicio = m.DataInicio!.Value;
        c.DataFim = m.DataFim!.Value;
        c.Ativo = m.Ativo;
    }

    private static ContratoServico NovoItem(ContratoItemViewModel i) => new()
    {
        TipoServicoId = i.TipoServicoId!.Value,
        TipoMedicaoId = i.TipoMedicaoId!.Value,
        Quantidade = i.Quantidade!.Value,
        ValorUnitario = i.ValorUnitario!.Value,
        Limite = i.Limite
    };

    private static decimal TotalDe(IEnumerable<ContratoServico> itens) =>
        Math.Round(itens.Sum(i => i.Quantidade * i.ValorUnitario), 2);

    private async Task<string?> SalvarPdfAsync(IFormFile? arquivo, CancellationToken ct)
    {
        if (arquivo is not { Length: > 0 }) return null;
        await using var s = arquivo.OpenReadStream();
        return await _storage.SalvarAsync(s, arquivo.FileName, "contratos", ct);
    }

    private async Task<IActionResult> FormularioAsync(ContratoFormViewModel model, string titulo, CancellationToken ct)
    {
        model.Fornecedores = await _db.Fornecedores.AsNoTracking()
            .Where(f => f.Ativo || f.Id == model.FornecedorId).OrderBy(f => f.RazaoSocial)
            .Select(f => new SelectListItem(f.RazaoSocial, f.Id.ToString())).ToListAsync(ct);
        model.Setores = await _db.Setores.AsNoTracking().Where(s => s.Ativo).OrderBy(s => s.Codigo)
            .Select(s => new SelectListItem(s.Codigo + " — " + s.Descricao, s.Id.ToString())).ToListAsync(ct);
        model.Servicos = await _db.TiposServico.AsNoTracking().Where(t => t.Ativo).OrderBy(t => t.Codigo)
            .Select(t => new SelectListItem(t.Codigo + " — " + t.Descricao, t.Id.ToString())).ToListAsync(ct);
        model.Medicoes = await _db.TiposMedicao.AsNoTracking().Where(t => t.Ativo).OrderBy(t => t.Codigo)
            .Select(t => new SelectListItem(t.Codigo + " — " + t.Descricao, t.Id.ToString())).ToListAsync(ct);

        ViewData["Title"] = titulo;
        return View("Form", model);
    }

    private async Task ValidarAsync(ContratoFormViewModel m, Contrato? atual, CancellationToken ct)
    {
        // Linhas totalmente vazias são ignoradas (o usuário só adicionou e não preencheu).
        m.Itens.RemoveAll(i => i.TipoServicoId is null && i.TipoMedicaoId is null
                            && i.Quantidade is null && i.ValorUnitario is null && i.Limite is null);

        var codigo = m.Codigo?.Trim().ToUpperInvariant() ?? string.Empty;
        if (codigo.Length > 0 && await _db.Contratos.AnyAsync(c => c.Codigo.ToUpper() == codigo && c.Id != m.Id, ct))
            ModelState.AddModelError(nameof(m.Codigo), "Já existe um contrato com este código.");

        var fornecedorAtual = atual?.FornecedorId ?? 0;
        if (m.FornecedorId is int fid && !await _db.Fornecedores.AnyAsync(f => f.Id == fid && (f.Ativo || f.Id == fornecedorAtual), ct))
            ModelState.AddModelError(nameof(m.FornecedorId), "Fornecedor inválido ou inativo.");

        if (m.SetorId is int sid && !await _db.Setores.AnyAsync(s => s.Id == sid, ct))
            ModelState.AddModelError(nameof(m.SetorId), "Setor inválido.");

        if (m.DataInicio is not null && m.DataFim is not null && m.DataFim < m.DataInicio)
            ModelState.AddModelError(nameof(m.DataFim), "O fim da vigência não pode ser anterior ao início.");

        if (m.ArquivoPdf is { Length: > 0 })
        {
            var erro = await UploadValidator.ValidarContratoPdfAsync(m.ArquivoPdf, ct);
            if (erro is not null) ModelState.AddModelError(nameof(m.ArquivoPdf), erro);
        }

        if (m.Itens.Count == 0)
            ModelState.AddModelError(string.Empty, "Adicione ao menos um serviço ao contrato.");

        var servicosValidos = await _db.TiposServico.Select(t => t.Id).ToListAsync(ct);
        var medicoesValidas = await _db.TiposMedicao.Select(t => t.Id).ToListAsync(ct);
        var executado = atual is null ? new Dictionary<int, decimal>()
            : await ConsumoContratos.QuantidadePorItemAsync(_db, atual.Id, ct);

        for (var n = 0; n < m.Itens.Count; n++)
        {
            var i = m.Itens[n];
            var rotulo = $"Serviço {n + 1}";

            if (i.TipoServicoId is null || !servicosValidos.Contains(i.TipoServicoId.Value))
                ModelState.AddModelError(string.Empty, $"{rotulo}: selecione o tipo de serviço.");
            if (i.TipoMedicaoId is null || !medicoesValidas.Contains(i.TipoMedicaoId.Value))
                ModelState.AddModelError(string.Empty, $"{rotulo}: selecione o tipo de medição.");
            if (i.Quantidade is null or <= 0)
                ModelState.AddModelError(string.Empty, $"{rotulo}: a quantidade deve ser maior que zero.");
            if (i.ValorUnitario is null or <= 0)
                ModelState.AddModelError(string.Empty, $"{rotulo}: o valor unitário deve ser maior que zero.");
            if (i.Limite is <= 0)
                ModelState.AddModelError(string.Empty, $"{rotulo}: o limite, quando informado, deve ser maior que zero.");

            if (atual is not null && i.Id > 0)
            {
                var existente = atual.Servicos.FirstOrDefault(s => s.Id == i.Id);
                if (existente is null)
                {
                    ModelState.AddModelError(string.Empty, $"{rotulo}: item inexistente neste contrato.");
                }
                else if (executado.TryGetValue(i.Id, out var usado) && usado > 0)
                {
                    if (i.TipoServicoId != existente.TipoServicoId || i.TipoMedicaoId != existente.TipoMedicaoId)
                        ModelState.AddModelError(string.Empty, $"{rotulo}: já foi usado em atividades — serviço e medição não podem ser trocados.");
                    if (i.Quantidade < usado)
                        ModelState.AddModelError(string.Empty, $"{rotulo}: a quantidade não pode ficar abaixo do já executado ({usado:0.####}).");
                }
            }
        }

        var repetidos = m.Itens.Where(i => i.TipoServicoId is not null).GroupBy(i => i.TipoServicoId).Any(g => g.Count() > 1);
        if (repetidos)
            ModelState.AddModelError(string.Empty, "O mesmo tipo de serviço não pode aparecer duas vezes no contrato.");

        if (atual is not null)
        {
            var enviados = m.Itens.Where(i => i.Id > 0).Select(i => i.Id).ToHashSet();
            foreach (var removido in atual.Servicos.Where(s => !enviados.Contains(s.Id)))
            {
                if (executado.TryGetValue(removido.Id, out var usado) && usado > 0)
                    ModelState.AddModelError(string.Empty, "Um serviço já usado em atividades não pode ser removido do contrato.");
            }
        }
    }
}

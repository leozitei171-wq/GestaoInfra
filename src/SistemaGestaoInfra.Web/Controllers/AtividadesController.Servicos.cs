using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Common;
using SistemaGestaoInfra.Domain.Entities;
using SistemaGestaoInfra.Infra.Data;
using SistemaGestaoInfra.Web.Services;

namespace SistemaGestaoInfra.Web.Controllers;

/// <summary>Lançamento dos serviços de contrato executados em uma atividade (gera custo e consumo).</summary>
public partial class AtividadesController
{
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.GestaoCadastros)]
    public async Task<IActionResult> LancarServico(
        int id, int contratoServicoId,
        [ModelBinder(typeof(DecimalFlexivelModelBinder))] decimal quantidade,
        CancellationToken ct)
    {
        var a = await CarregarAsync(id, ct);
        if (a is null) return NotFound();

        if (a.Status!.Codigo == StatusCodigos.Negada)
            return Recusar(id, "Atividades negadas não podem receber serviços de contrato.");

        if (quantidade <= 0)
            return Recusar(id, "A quantidade deve ser maior que zero.");

        var item = await _db.ContratoServicos.Include(i => i.Contrato)
            .FirstOrDefaultAsync(i => i.Id == contratoServicoId, ct);
        if (item?.Contrato is null)
            return Recusar(id, "Serviço de contrato inválido.");

        var hoje = DateOnly.FromDateTime(DateTime.Now);
        if (!item.Contrato.Ativo || hoje < item.Contrato.DataInicio || hoje > item.Contrato.DataFim)
            return Recusar(id, $"O contrato {item.Contrato.Codigo} não está ativo e vigente hoje.");

        var executado = (await ConsumoContratos.QuantidadePorItemAsync(_db, item.ContratoId, ct))
            .GetValueOrDefault(item.Id);
        var teto = item.Limite ?? item.Quantidade;
        var saldo = teto - executado;
        if (quantidade > saldo)
            return Recusar(id, $"Quantidade acima do saldo disponível ({Math.Max(saldo, 0):0.####}) para este serviço do contrato.");

        _db.AtividadeServicos.Add(new AtividadeServico
        {
            AtividadeId = a.Id,
            ContratoServicoId = item.Id,
            Quantidade = quantidade,
            ValorUnitario = item.ValorUnitario,   // congela o preço do momento
            PrefeituraId = a.PrefeituraId
        });
        await _db.SaveChangesAsync(ct);

        // Reavalia o contrato já: se cruzou 80%/100% o alerta aparece sem esperar o ciclo do motor.
        await AlertasContrato.AtualizarAsync(_db, [item.ContratoId], hoje, ct);

        TempData["Sucesso"] = $"Serviço lançado: {quantidade:0.####} × {item.ValorUnitario:C2} = {quantidade * item.ValorUnitario:C2}.";
        return RedirectToAction(nameof(Detalhe), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.GestaoCadastros)]
    public async Task<IActionResult> RemoverServico(int id, int servicoId, CancellationToken ct)
    {
        var lancamento = await _db.AtividadeServicos
            .FirstOrDefaultAsync(s => s.Id == servicoId && s.AtividadeId == id, ct);
        if (lancamento is null) return NotFound();

        _db.AtividadeServicos.Remove(lancamento);
        await _db.SaveChangesAsync(ct);

        TempData["Sucesso"] = "Lançamento de serviço removido.";
        return RedirectToAction(nameof(Detalhe), new { id });
    }
}

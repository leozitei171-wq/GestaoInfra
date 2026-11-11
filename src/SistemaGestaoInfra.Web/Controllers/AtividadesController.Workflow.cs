using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Common;
using SistemaGestaoInfra.Domain.Entities;

namespace SistemaGestaoInfra.Web.Controllers;

/// <summary>
/// Ações do ciclo de vida da atividade. Toda transição é validada no servidor pelo
/// <see cref="FluxoAtividade"/>; esconder o botão na tela não é a única barreira.
/// </summary>
public partial class AtividadesController
{
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.GestaoCadastros)]
    public async Task<IActionResult> Programar(int id, DateOnly? inicio, DateOnly? fim, CancellationToken ct)
    {
        var a = await CarregarAsync(id, ct);
        if (a is null) return NotFound();

        var atual = a.Status!.Codigo;
        if (!FluxoAtividade.PodeProgramar(atual))
            return Recusar(id, "Esta atividade não pode mais ser programada.");

        if (fim is null)
            return Recusar(id, "Informe a data de término prevista.");

        if (inicio is not null && inicio > fim)
            return Recusar(id, "A data de início não pode ser posterior à data de término.");

        // Um prazo já vencido faria o motor marcar a atividade como atrasada de imediato.
        if (fim < DateOnly.FromDateTime(DateTime.Now))
            return Recusar(id, "A data de término não pode estar no passado.");

        var jaIniciada = a.DataInicioReal is not null;
        var novoStatus = FluxoAtividade.StatusAposProgramar(atual, jaIniciada);

        a.DataInicioPrevisto = inicio;
        a.DataFimPrevisto = fim;
        if (!await DefinirStatusAsync(a, novoStatus, ct))
            return Recusar(id, "Status padrão não configurado. Fale com o administrador.");

        await _db.SaveChangesAsync(ct);
        TempData["Sucesso"] = $"Atividade #{id} programada até {fim:dd/MM/yyyy}.";
        return RedirectToAction(nameof(Detalhe), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.Execucao)]
    public async Task<IActionResult> Iniciar(int id, CancellationToken ct)
    {
        var a = await CarregarAsync(id, ct);
        if (a is null) return NotFound();

        var atual = a.Status!.Codigo;
        if (!FluxoAtividade.PodeIniciar(atual, a.DataInicioReal is not null))
            return Recusar(id, "A atividade precisa estar programada para ser iniciada.");

        a.DataInicioReal = DateTime.UtcNow;

        // Atrasada e ainda não iniciada: registra o início mas mantém "Atrasada" (o prazo já venceu).
        if (atual == StatusCodigos.Programada &&
            !await DefinirStatusAsync(a, StatusCodigos.EmExecucao, ct))
            return Recusar(id, "Status padrão não configurado. Fale com o administrador.");

        await _db.SaveChangesAsync(ct);
        TempData["Sucesso"] = $"Execução da atividade #{id} iniciada.";
        return RedirectToAction(nameof(Detalhe), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.Execucao)]
    public async Task<IActionResult> Progresso(int id, int progresso, CancellationToken ct)
    {
        var a = await CarregarAsync(id, ct);
        if (a is null) return NotFound();

        if (!FluxoAtividade.PodeAtualizarProgresso(a.Status!.Codigo, a.DataInicioReal is not null))
            return Recusar(id, "Só é possível atualizar o progresso de atividades em execução.");

        if (progresso is < 0 or > 99)
            return Recusar(id, "O progresso deve estar entre 0% e 99%. Para 100%, conclua a atividade.");

        a.Progresso = progresso;
        await _db.SaveChangesAsync(ct);
        TempData["Sucesso"] = $"Progresso da atividade #{id} atualizado para {progresso}%.";
        return RedirectToAction(nameof(Detalhe), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.Execucao)]
    public async Task<IActionResult> Concluir(int id, CancellationToken ct)
    {
        var a = await CarregarAsync(id, ct);
        if (a is null) return NotFound();

        if (!FluxoAtividade.PodeConcluir(a.Status!.Codigo))
            return Recusar(id, "Só é possível concluir atividades em execução ou atrasadas.");

        if (!await DefinirStatusAsync(a, StatusCodigos.Concluida, ct))
            return Recusar(id, "Status padrão não configurado. Fale com o administrador.");

        a.Progresso = 100;
        a.DataConclusao = DateTime.UtcNow;
        a.DataInicioReal ??= a.DataConclusao;

        await _db.SaveChangesAsync(ct);
        TempData["Sucesso"] = $"Atividade #{id} concluída.";
        return RedirectToAction(nameof(Detalhe), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.GestaoCadastros)]
    public async Task<IActionResult> Negar(int id, string? motivo, CancellationToken ct)
    {
        var a = await CarregarAsync(id, ct);
        if (a is null) return NotFound();

        if (!FluxoAtividade.PodeNegar(a.Status!.Codigo))
            return Recusar(id, "Só é possível negar atividades abertas ou programadas.");

        motivo = motivo?.Trim();
        if (string.IsNullOrEmpty(motivo) || motivo.Length < 5)
            return Recusar(id, "Informe o motivo da negativa (mínimo de 5 caracteres).");

        if (!await DefinirStatusAsync(a, StatusCodigos.Negada, ct))
            return Recusar(id, "Status padrão não configurado. Fale com o administrador.");

        a.MotivoNegativa = motivo.Length > 500 ? motivo[..500] : motivo;

        await _db.SaveChangesAsync(ct);
        TempData["Sucesso"] = $"Atividade #{id} negada.";
        return RedirectToAction(nameof(Detalhe), new { id });
    }

    private Task<Atividade?> CarregarAsync(int id, CancellationToken ct) =>
        _db.Atividades.Include(a => a.Status).FirstOrDefaultAsync(a => a.Id == id, ct);

    private IActionResult Recusar(int id, string mensagem)
    {
        TempData["Erro"] = mensagem;
        return RedirectToAction(nameof(Detalhe), new { id });
    }

    /// <summary>
    /// Busca o status pelo código na prefeitura DA ATIVIDADE (não do usuário: o admin enxerga todas).
    /// </summary>
    private async Task<bool> DefinirStatusAsync(Atividade a, string codigo, CancellationToken ct)
    {
        var statusId = await _db.StatusAtividades
            .Where(s => s.PrefeituraId == a.PrefeituraId && s.Codigo == codigo)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);

        if (statusId is null) return false;

        a.StatusId = statusId.Value;
        a.Status = null; // evita que o EF reponha o status antigo pela propriedade de navegação
        return true;
    }
}

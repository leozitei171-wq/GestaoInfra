using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Common;

namespace SistemaGestaoInfra.Infra.Data;

/// <summary>
/// Cálculo único de quanto cada contrato (e cada item) já foi consumido pelas atividades.
/// Atividades negadas não consomem contrato. Respeita o filtro multi-tenant do contexto.
/// </summary>
public static class ConsumoContratos
{
    /// <summary>Valor consumido (quantidade x valor unitário do lançamento) por contrato.</summary>
    public static async Task<Dictionary<int, decimal>> ValorPorContratoAsync(AppDbContext db, CancellationToken ct)
    {
        var linhas = await (
            from a in db.AtividadeServicos.AsNoTracking()
            join i in db.ContratoServicos.AsNoTracking() on a.ContratoServicoId equals i.Id
            where a.Atividade!.Status!.Codigo != StatusCodigos.Negada
            group a by i.ContratoId into g
            select new { ContratoId = g.Key, Valor = g.Sum(x => x.Quantidade * x.ValorUnitario) }
        ).ToListAsync(ct);

        return linhas.ToDictionary(l => l.ContratoId, l => l.Valor);
    }

    /// <summary>Quantidade já executada por item de contrato.</summary>
    public static async Task<Dictionary<int, decimal>> QuantidadePorItemAsync(AppDbContext db, int contratoId, CancellationToken ct)
    {
        var linhas = await (
            from a in db.AtividadeServicos.AsNoTracking()
            where a.ContratoServico!.ContratoId == contratoId
               && a.Atividade!.Status!.Codigo != StatusCodigos.Negada
            group a by a.ContratoServicoId into g
            select new { ItemId = g.Key, Quantidade = g.Sum(x => x.Quantidade) }
        ).ToListAsync(ct);

        return linhas.ToDictionary(l => l.ItemId, l => l.Quantidade);
    }

    /// <summary>Quantidade já executada de TODOS os itens (usado para calcular saldos).</summary>
    public static async Task<Dictionary<int, decimal>> QuantidadePorItemAsync(AppDbContext db, CancellationToken ct)
    {
        var linhas = await (
            from a in db.AtividadeServicos.AsNoTracking()
            where a.Atividade!.Status!.Codigo != StatusCodigos.Negada
            group a by a.ContratoServicoId into g
            select new { ItemId = g.Key, Quantidade = g.Sum(x => x.Quantidade) }
        ).ToListAsync(ct);

        return linhas.ToDictionary(l => l.ItemId, l => l.Quantidade);
    }
}

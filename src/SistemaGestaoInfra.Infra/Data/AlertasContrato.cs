using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Common;
using SistemaGestaoInfra.Domain.Entities;

namespace SistemaGestaoInfra.Infra.Data;

/// <summary>
/// Gera alertas quando um contrato ativo atinge os limiares de uso ou se aproxima do vencimento.
/// Cada combinação (contrato, nível) gera no máximo um alerta; uma escalada de "atenção" para
/// "crítico" gera um novo. Funciona para todas as prefeituras (usado pelo motor em segundo plano).
/// </summary>
public static class AlertasContrato
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

    /// <param name="contratoIds">Restringe a avaliação a estes contratos; nulo avalia todos os ativos.</param>
    /// <returns>Quantidade de alertas criados.</returns>
    public static async Task<int> AtualizarAsync(
        AppDbContext db, IReadOnlyCollection<int>? contratoIds, DateOnly hoje, CancellationToken ct)
    {
        var query = db.Contratos.IgnoreQueryFilters().AsNoTracking().Where(c => c.Ativo);
        if (contratoIds is not null)
        {
            query = query.Where(c => contratoIds.Contains(c.Id));
        }

        var contratos = await query
            .Select(c => new { c.Id, c.PrefeituraId, c.Codigo, c.ValorTotal, c.DataInicio, c.DataFim })
            .ToListAsync(ct);
        if (contratos.Count == 0) return 0;

        var ids = contratos.Select(c => c.Id).ToList();

        // Consumo por contrato, ignorando o filtro de tenant (cobre todas as prefeituras).
        var consumo = (await (
                from a in db.AtividadeServicos.IgnoreQueryFilters().AsNoTracking()
                join i in db.ContratoServicos.IgnoreQueryFilters().AsNoTracking() on a.ContratoServicoId equals i.Id
                where ids.Contains(i.ContratoId) && a.Atividade!.Status!.Codigo != StatusCodigos.Negada
                group a by i.ContratoId into g
                select new { ContratoId = g.Key, Valor = g.Sum(x => x.Quantidade * x.ValorUnitario) })
            .ToListAsync(ct))
            .ToDictionary(x => x.ContratoId, x => x.Valor);

        var jaGerados = (await db.ContratoAlertas.IgnoreQueryFilters().AsNoTracking()
                .Where(a => ids.Contains(a.ContratoId))
                .Select(a => new { a.ContratoId, a.Nivel })
                .ToListAsync(ct))
            .Select(a => (a.ContratoId, a.Nivel))
            .ToHashSet();

        var criados = 0;
        foreach (var c in contratos)
        {
            var uso = c.ValorTotal <= 0 ? 0 : Math.Round(consumo.GetValueOrDefault(c.Id) * 100 / c.ValorTotal, 2);

            // Uso: dispara o nível correspondente (e o "atenção" também conta quando já está crítico).
            var nivelUso = NivelAlerta.PorUso(uso);
            if (nivelUso is not null && jaGerados.Add((c.Id, nivelUso)))
            {
                db.ContratoAlertas.Add(Novo(c.PrefeituraId, c.Id, nivelUso, uso, nivelUso == NivelAlerta.Critico
                    ? $"Contrato {c.Codigo} atingiu {uso.ToString("0.##", Pt)}% de uso — limite contratado atingido ou ultrapassado."
                    : $"Contrato {c.Codigo} atingiu {uso.ToString("0.##", Pt)}% de uso."));
                criados++;
            }

            // Vencimento: contrato vigente que termina nos próximos dias.
            var dias = c.DataFim.DayNumber - hoje.DayNumber;
            if (dias >= 0 && dias <= NivelAlerta.DiasAvisoVencimento && jaGerados.Add((c.Id, NivelAlerta.Vencimento)))
            {
                db.ContratoAlertas.Add(Novo(c.PrefeituraId, c.Id, NivelAlerta.Vencimento, uso,
                    dias == 0
                        ? $"Contrato {c.Codigo} vence hoje ({c.DataFim:dd/MM/yyyy})."
                        : $"Contrato {c.Codigo} vence em {dias} dia(s) ({c.DataFim:dd/MM/yyyy})."));
                criados++;
            }
        }

        if (criados > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return criados;
    }

    private static ContratoAlerta Novo(int prefeituraId, int contratoId, string nivel, decimal uso, string mensagem) => new()
    {
        PrefeituraId = prefeituraId,
        ContratoId = contratoId,
        Nivel = nivel,
        PercentualUso = uso,
        Mensagem = mensagem
    };
}

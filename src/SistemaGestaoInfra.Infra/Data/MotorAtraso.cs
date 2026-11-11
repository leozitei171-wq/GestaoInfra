using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Common;

namespace SistemaGestaoInfra.Infra.Data;

/// <summary>
/// Marca como ATRASADA toda atividade programada ou em execução cujo prazo (data fim prevista)
/// já passou. Roda para todas as prefeituras, por isso ignora o filtro multi-tenant.
/// </summary>
public static class MotorAtraso
{
    private const int TamanhoLote = 200;

    /// <returns>Quantidade de atividades marcadas como atrasadas.</returns>
    public static async Task<int> ExecutarAsync(AppDbContext db, DateOnly hoje, CancellationToken ct)
    {
        var total = 0;

        // Status ATRASADA de cada prefeitura (o id é diferente por tenant).
        var atrasadaPorPrefeitura = await db.StatusAtividades
            .IgnoreQueryFilters()
            .Where(s => s.Codigo == StatusCodigos.Atrasada)
            .ToDictionaryAsync(s => s.PrefeituraId, s => s.Id, ct);

        while (!ct.IsCancellationRequested)
        {
            var lote = await db.Atividades
                .IgnoreQueryFilters()
                .Where(a => a.DataFimPrevisto != null && a.DataFimPrevisto < hoje)
                .Where(a => a.Status!.Codigo == StatusCodigos.Programada
                         || a.Status.Codigo == StatusCodigos.EmExecucao)
                .OrderBy(a => a.Id)
                .Take(TamanhoLote)
                .ToListAsync(ct);

            if (lote.Count == 0)
            {
                break;
            }

            var alterou = false;
            foreach (var atividade in lote)
            {
                if (atrasadaPorPrefeitura.TryGetValue(atividade.PrefeituraId, out var statusId))
                {
                    atividade.StatusId = statusId;
                    atividade.Status = null;
                    alterou = true;
                    total++;
                }
            }

            if (!alterou)
            {
                break; // prefeituras sem o status ATRASADA configurado: evita laço infinito
            }

            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }

        return total;
    }
}

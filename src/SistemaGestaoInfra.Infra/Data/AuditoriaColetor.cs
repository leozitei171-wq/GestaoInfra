using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SistemaGestaoInfra.Domain.Common;
using SistemaGestaoInfra.Domain.Entities;

namespace SistemaGestaoInfra.Infra.Data;

internal sealed record AuditoriaPendente(
    EntityEntry Entry, string Acao, string Entidade, string? ChaveAntes, string Payload);

/// <summary>
/// Lê o ChangeTracker antes do SaveChanges e descreve cada criação, alteração e exclusão.
/// </summary>
internal static class AuditoriaColetor
{
    private const int TamanhoMaxPayload = 8000;

    // Campos que mudam sozinhos (carimbos, contadores) e só gerariam ruído na trilha.
    private static readonly HashSet<string> Ruido = new(StringComparer.Ordinal)
    {
        "CriadoEm", "AtualizadoEm", "ConcurrencyStamp", "SecurityStamp",
        "AccessFailedCount", "LockoutEnd", "NormalizedUserName", "NormalizedEmail"
    };

    // Campos cujo VALOR nunca pode ser gravado na auditoria (só o fato de terem mudado).
    private static readonly HashSet<string> Sensiveis = new(StringComparer.Ordinal) { "PasswordHash" };

    private static readonly JsonSerializerOptions Json = new()
    {
        // O payload é sempre exibido como texto (Razor codifica), então acentos podem ficar legíveis.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static List<AuditoriaPendente> Coletar(ChangeTracker tracker)
    {
        tracker.DetectChanges();
        var resultado = new List<AuditoriaPendente>();

        var entradas = tracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is EntidadeBase or ApplicationUser)
            .ToList();

        foreach (var entry in entradas)
        {
            Dictionary<string, object?> dados;
            string acao;
            string raiz;

            switch (entry.State)
            {
                case EntityState.Added:
                    acao = "Criou";
                    raiz = "novos";
                    dados = ValoresAtuais(entry, entry.CurrentValues);
                    break;

                case EntityState.Modified:
                    acao = "Alterou";
                    raiz = "alteracoes";
                    dados = Alteracoes(entry);
                    if (dados.Count == 0)
                    {
                        continue; // só ruído mudou (ex.: carimbo de segurança)
                    }
                    break;

                default:
                    acao = "Excluiu";
                    raiz = "excluidos";
                    dados = ValoresAtuais(entry, entry.OriginalValues);
                    break;
            }

            var payload = JsonSerializer.Serialize(new Dictionary<string, object?> { [raiz] = dados }, Json);
            if (payload.Length > TamanhoMaxPayload)
            {
                payload = payload[..TamanhoMaxPayload] + "…(truncado)";
            }

            resultado.Add(new AuditoriaPendente(
                entry,
                acao,
                NomeEntidade(entry),
                entry.State == EntityState.Added ? null : Chave(entry),
                payload));
        }

        return resultado;
    }

    public static string NomeEntidade(EntityEntry entry) =>
        entry.Entity is ApplicationUser ? "Usuario" : entry.Metadata.ClrType.Name;

    public static string? Chave(EntityEntry entry)
    {
        var nome = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault()?.Name;
        return nome is null
            ? null
            : Convert.ToString(entry.Property(nome).CurrentValue, CultureInfo.InvariantCulture);
    }

    public static int? PrefeituraDe(EntityEntry entry) => entry.Entity switch
    {
        EntidadeTenant t => t.PrefeituraId,
        ApplicationUser u => u.PrefeituraId,
        Prefeitura p => p.Id,
        _ => null
    };

    private static Dictionary<string, object?> ValoresAtuais(EntityEntry entry, PropertyValues valores)
    {
        var dados = new Dictionary<string, object?>();
        foreach (var p in entry.Metadata.GetProperties())
        {
            if (Ruido.Contains(p.Name) || Sensiveis.Contains(p.Name) || p.IsPrimaryKey())
            {
                continue;
            }

            dados[p.Name] = valores[p];
        }

        return dados;
    }

    private static Dictionary<string, object?> Alteracoes(EntityEntry entry)
    {
        var dados = new Dictionary<string, object?>();
        foreach (var p in entry.Properties.Where(p => p.IsModified))
        {
            var nome = p.Metadata.Name;
            if (Ruido.Contains(nome) || Equals(p.OriginalValue, p.CurrentValue))
            {
                continue;
            }

            dados[nome] = Sensiveis.Contains(nome)
                ? "(alterado)"
                : new Dictionary<string, object?> { ["de"] = p.OriginalValue, ["para"] = p.CurrentValue };
        }

        return dados;
    }
}

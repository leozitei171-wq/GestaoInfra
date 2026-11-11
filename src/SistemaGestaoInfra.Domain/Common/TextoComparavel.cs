using System.Globalization;
using System.Text;

namespace SistemaGestaoInfra.Domain.Common;

/// <summary>Comparação de textos ignorando acentos, caixa e espaços repetidos.</summary>
public static class TextoComparavel
{
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var decomposto = texto.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        var ultimoFoiEspaco = false;

        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;   // remove acentos

            if (char.IsWhiteSpace(c) || c == '-')
            {
                if (!ultimoFoiEspaco) sb.Append(' ');
                ultimoFoiEspaco = true;
            }
            else
            {
                sb.Append(char.ToLowerInvariant(c));
                ultimoFoiEspaco = false;
            }
        }

        return sb.ToString().Trim();
    }

    public static bool Iguais(string? a, string? b) =>
        Normalizar(a).Length > 0 && Normalizar(a) == Normalizar(b);
}

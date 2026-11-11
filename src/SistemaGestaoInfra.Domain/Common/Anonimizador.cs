using System.Text.RegularExpressions;

namespace SistemaGestaoInfra.Domain.Common;

/// <summary>
/// Oculta dados pessoais comuns (e-mail, CPF, telefone) em textos livres antes de exibi-los ao público.
/// É uma proteção de melhor esforço: não substitui orientar quem registra a não digitar dados pessoais.
/// Nomes de pessoas, por exemplo, não são detectáveis por padrão.
/// </summary>
public static class Anonimizador
{
    // Prazo curto em todas as expressões: entrada hostil não pode travar o servidor (ReDoS).
    private static readonly TimeSpan Prazo = TimeSpan.FromMilliseconds(100);

    private static readonly Regex Email = new(
        @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant, Prazo);

    // 000.000.000-00, 00000000000 ou variações com ponto/hífen.
    private static readonly Regex Cpf = new(
        @"(?<!\d)\d{3}\.?\d{3}\.?\d{3}-?\d{2}(?!\d)", RegexOptions.CultureInvariant, Prazo);

    // (16) 99999-9999, 16 3333-4444, +55 16 99999 9999, 99999-9999, 3333-4444
    private static readonly Regex Telefone = new(
        @"(?<!\d)(?:\+?55[\s\-]?)?(?:\(?\d{2}\)?[\s\-]?)?9?\d{4}[\s\-]?\d{4}(?!\d)", RegexOptions.CultureInvariant, Prazo);

    public static string Mascarar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;

        try
        {
            var r = Email.Replace(texto, "[e-mail oculto]");
            r = Cpf.Replace(r, "[CPF oculto]");        // antes do telefone: CPF tem 11 dígitos e casaria como telefone
            r = Telefone.Replace(r, "[telefone oculto]");
            return r;
        }
        catch (RegexMatchTimeoutException)
        {
            // Na dúvida, não publica o texto.
            return "[texto indisponível para exibição pública]";
        }
    }

    /// <summary>Mascara e limita o tamanho (com reticências).</summary>
    public static string Resumir(string? texto, int max)
    {
        var m = Mascarar(texto);
        return m.Length <= max ? m : m[..max].TrimEnd() + "…";
    }
}

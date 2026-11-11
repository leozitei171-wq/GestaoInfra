namespace SistemaGestaoInfra.Web.Services;

/// <summary>
/// Valida uploads pelo conteúdo real (assinatura/magic bytes), tamanho e extensão.
/// Não confia no Content-Type nem no nome enviados pelo cliente.
/// </summary>
public static class UploadValidator
{
    public const long TamanhoMaxFoto = 5L * 1024 * 1024;
    public const long TamanhoMaxRequerimento = 15L * 1024 * 1024;

    private static readonly byte[] AssinaturaJpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] AssinaturaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] AssinaturaPdf = [0x25, 0x50, 0x44, 0x46, 0x2D]; // %PDF-

    /// <summary>Foto de evidência: JPEG ou PNG, até 5 MB.</summary>
    public static Task<string?> ValidarFotoAsync(IFormFile arquivo, CancellationToken ct) =>
        ValidarAsync(arquivo, TamanhoMaxFoto, permitirPdf: false, "A foto", ct);

    /// <summary>Anexo de requerimento: PDF, JPEG ou PNG, até 15 MB.</summary>
    public static Task<string?> ValidarRequerimentoAsync(IFormFile arquivo, CancellationToken ct) =>
        ValidarAsync(arquivo, TamanhoMaxRequerimento, permitirPdf: true, "O anexo", ct);

    public const long TamanhoMaxContrato = 20L * 1024 * 1024;

    /// <summary>PDF do contrato: somente PDF, até 20 MB, conferido pela assinatura "%PDF-".</summary>
    public static async Task<string?> ValidarContratoPdfAsync(IFormFile arquivo, CancellationToken ct)
    {
        if (arquivo.Length == 0) return "O PDF está vazio.";
        if (arquivo.Length > TamanhoMaxContrato) return $"O PDF excede o tamanho máximo de {TamanhoMaxContrato / 1024 / 1024} MB.";
        if (!string.Equals(Path.GetExtension(arquivo.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            return "O contrato deve ser um arquivo PDF.";

        var cabecalho = new byte[5];
        await using var stream = arquivo.OpenReadStream();
        var lidos = await stream.ReadAsync(cabecalho.AsMemory(0, cabecalho.Length), ct);

        return lidos == cabecalho.Length && cabecalho.AsSpan().SequenceEqual(AssinaturaPdf)
            ? null
            : "O arquivo não é um PDF válido (conteúdo não corresponde à extensão).";
    }

    /// <summary>Retorna a mensagem de erro, ou null quando o arquivo é válido.</summary>
    private static async Task<string?> ValidarAsync(
        IFormFile arquivo, long tamanhoMax, bool permitirPdf, string rotulo, CancellationToken ct)
    {
        if (arquivo.Length == 0)
        {
            return $"{rotulo} está vazio.";
        }

        if (arquivo.Length > tamanhoMax)
        {
            return $"{rotulo} excede o tamanho máximo de {tamanhoMax / 1024 / 1024} MB.";
        }

        var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        var extensoesOk = permitirPdf
            ? new[] { ".jpg", ".jpeg", ".png", ".pdf" }
            : new[] { ".jpg", ".jpeg", ".png" };

        if (!extensoesOk.Contains(extensao))
        {
            return permitirPdf
                ? $"{rotulo} deve ser PDF, JPEG ou PNG."
                : $"{rotulo} deve ser JPEG ou PNG.";
        }

        var cabecalho = new byte[8];
        await using (var stream = arquivo.OpenReadStream())
        {
            var lidos = await stream.ReadAsync(cabecalho.AsMemory(0, cabecalho.Length), ct);
            if (lidos < 3)
            {
                return $"{rotulo} está corrompido.";
            }
        }

        var ehJpeg = cabecalho.AsSpan().StartsWith(AssinaturaJpeg);
        var ehPng = cabecalho.AsSpan().StartsWith(AssinaturaPng);
        var ehPdf = permitirPdf && cabecalho.AsSpan().StartsWith(AssinaturaPdf);

        if (!ehJpeg && !ehPng && !ehPdf)
        {
            return $"{rotulo} não é um arquivo válido (conteúdo não corresponde à extensão).";
        }

        // A extensão declarada precisa ser coerente com o conteúdo real.
        var coerente = extensao switch
        {
            ".jpg" or ".jpeg" => ehJpeg,
            ".png" => ehPng,
            ".pdf" => ehPdf,
            _ => false
        };

        return coerente ? null : $"{rotulo} tem extensão diferente do conteúdo real.";
    }
}

using Microsoft.Extensions.Options;
using SistemaGestaoInfra.Application.Common.Interfaces;

namespace SistemaGestaoInfra.Infra.Storage;

/// <summary>
/// Opções de configuração do armazenamento local de arquivos.
/// </summary>
public class LocalFileStorageOptions
{
    /// <summary>Caminho físico raiz onde os arquivos são gravados.</summary>
    public string RaizFisica { get; set; } = string.Empty;

    /// <summary>Prefixo da URL pública que serve os arquivos (ex: /uploads).</summary>
    public string PrefixoUrl { get; set; } = "/uploads";
}

/// <summary>
/// Armazenamento de arquivos no filesystem local. Gera nomes únicos para evitar
/// colisões e valida a extensão contra uma allowlist.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private static readonly string[] ExtensoesPermitidas = [".jpg", ".jpeg", ".png", ".pdf"];

    private readonly LocalFileStorageOptions _options;

    public LocalFileStorage(IOptions<LocalFileStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> SalvarAsync(
        Stream conteudo,
        string nomeOriginal,
        string pasta,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        var extensao = Path.GetExtension(nomeOriginal).ToLowerInvariant();
        if (!ExtensoesPermitidas.Contains(extensao))
        {
            throw new InvalidOperationException($"Extensão de arquivo não permitida: {extensao}");
        }

        // Normaliza a subpasta para evitar path traversal.
        var pastaSegura = Path.GetFileName(pasta);
        if (string.IsNullOrWhiteSpace(pastaSegura))
        {
            throw new ArgumentException("Pasta de destino inválida.", nameof(pasta));
        }

        var nomeArquivo = $"{Guid.NewGuid():N}{extensao}";
        var caminhoRelativo = Path.Combine(pastaSegura, nomeArquivo).Replace('\\', '/');

        var diretorioFisico = Path.Combine(_options.RaizFisica, pastaSegura);
        Directory.CreateDirectory(diretorioFisico);

        var caminhoFisico = Path.Combine(diretorioFisico, nomeArquivo);

        await using var destino = File.Create(caminhoFisico);
        await conteudo.CopyToAsync(destino, ct);

        return caminhoRelativo;
    }

    public Task RemoverAsync(string caminhoRelativo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(caminhoRelativo))
        {
            return Task.CompletedTask;
        }

        var caminhoFisico = ResolverCaminhoFisico(caminhoRelativo);
        if (caminhoFisico is not null && File.Exists(caminhoFisico))
        {
            File.Delete(caminhoFisico);
        }

        return Task.CompletedTask;
    }

    public Task<Stream?> AbrirAsync(string caminhoRelativo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(caminhoRelativo))
        {
            return Task.FromResult<Stream?>(null);
        }

        var caminhoFisico = ResolverCaminhoFisico(caminhoRelativo);
        if (caminhoFisico is null || !File.Exists(caminhoFisico))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(caminhoFisico, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public string ObterUrlPublica(string caminhoRelativo)
    {
        if (string.IsNullOrWhiteSpace(caminhoRelativo))
        {
            return string.Empty;
        }

        return $"{_options.PrefixoUrl.TrimEnd('/')}/{caminhoRelativo.TrimStart('/')}";
    }

    /// <summary>
    /// Resolve o caminho físico garantindo que permanece dentro da raiz configurada.
    /// </summary>
    private string? ResolverCaminhoFisico(string caminhoRelativo)
    {
        // O separador final evita que "/uploads2" seja aceito como filho de "/uploads".
        var raiz = Path.GetFullPath(_options.RaizFisica)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var combinado = Path.GetFullPath(Path.Combine(raiz, caminhoRelativo));

        return combinado.StartsWith(raiz, StringComparison.OrdinalIgnoreCase)
            ? combinado
            : null;
    }
}

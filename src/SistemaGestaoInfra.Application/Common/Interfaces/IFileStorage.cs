namespace SistemaGestaoInfra.Application.Common.Interfaces;

/// <summary>
/// Abstração de armazenamento de arquivos (fotos de evidência, PDFs de requerimento).
/// Implementação atual usa o filesystem local; pode ser trocada por Azure Blob/S3
/// sem impacto nas camadas superiores.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Salva um arquivo e retorna o caminho relativo para persistir na entidade.
    /// </summary>
    /// <param name="conteudo">Stream com o conteúdo do arquivo.</param>
    /// <param name="nomeOriginal">Nome original enviado pelo usuário (usado para extrair a extensão).</param>
    /// <param name="pasta">Subpasta lógica (ex: "atividades", "requerimentos").</param>
    Task<string> SalvarAsync(Stream conteudo, string nomeOriginal, string pasta, CancellationToken ct = default);

    /// <summary>
    /// Remove um arquivo previamente salvo. Não lança se o arquivo não existir.
    /// </summary>
    Task RemoverAsync(string caminhoRelativo, CancellationToken ct = default);

    /// <summary>
    /// Abre um arquivo salvo para leitura. Retorna null se não existir.
    /// </summary>
    Task<Stream?> AbrirAsync(string caminhoRelativo, CancellationToken ct = default);

    /// <summary>
    /// Converte o caminho relativo armazenado em uma URL pública servível.
    /// </summary>
    string ObterUrlPublica(string caminhoRelativo);
}

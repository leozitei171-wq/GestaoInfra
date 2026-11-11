namespace SistemaGestaoInfra.Application.Common.Interfaces;

/// <summary>Quão preciso é o ponto encontrado.</summary>
public enum PrecisaoGeocodificacao
{
    /// <summary>Encontrou o número exato do imóvel.</summary>
    Endereco = 1,

    /// <summary>Encontrou a rua, mas não o número (ponto aproximado sobre a via).</summary>
    Via = 2,

    /// <summary>Só encontrou uma região (bairro, cidade). Ponto pouco útil para uma ocorrência.</summary>
    Regiao = 3
}

/// <summary>Um local encontrado, já separado em campos de endereço.</summary>
public record ResultadoGeocodificacao(
    double Latitude,
    double Longitude,
    string? Rua,
    string? Numero,
    string? Bairro,
    string? Cidade,
    string? Uf,
    string? Cep,
    string Descricao,
    PrecisaoGeocodificacao Precisao);

/// <summary>Dados digitados pelo usuário para localizar um endereço.</summary>
public record ConsultaEndereco(
    string? Rua,
    string? Numero,
    string? Bairro,
    string? Cep,
    string? Cidade,
    string? Uf);

/// <summary>
/// Converte endereço em coordenadas (e o contrário). A implementação atual usa o Nominatim
/// (OpenStreetMap), mas qualquer provedor pode ser trocado sem alterar o restante do sistema.
/// </summary>
public interface IGeocodificador
{
    /// <summary>Endereço → possíveis pontos (do mais provável para o menos). Lista vazia se não achar.</summary>
    Task<IReadOnlyList<ResultadoGeocodificacao>> BuscarAsync(ConsultaEndereco consulta, CancellationToken ct = default);

    /// <summary>Coordenadas → endereço. Nulo se o ponto não corresponde a nenhum endereço conhecido.</summary>
    Task<ResultadoGeocodificacao?> ReversoAsync(double latitude, double longitude, CancellationToken ct = default);
}

/// <summary>O provedor está fora do ar, lento, bloqueou a consulta ou devolveu algo ilegível.</summary>
public class GeocodificacaoIndisponivelException : Exception
{
    public GeocodificacaoIndisponivelException(string mensagem, Exception? interna = null) : base(mensagem, interna)
    {
    }
}

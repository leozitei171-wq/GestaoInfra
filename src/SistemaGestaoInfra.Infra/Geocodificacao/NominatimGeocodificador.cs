using System.Globalization;
using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaGestaoInfra.Application.Common.Interfaces;

namespace SistemaGestaoInfra.Infra.Geocodificacao;

public class NominatimOptions
{
    public const string Secao = "Geocodificacao";

    /// <summary>Endereço do serviço. Pode apontar para uma instância própria do Nominatim.</summary>
    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org";

    /// <summary>
    /// Identificação da aplicação. A política de uso do Nominatim público EXIGE um User-Agent que
    /// identifique a aplicação e permita contato; informe o seu e-mail em Geocodificacao:UserAgent.
    /// </summary>
    public string UserAgent { get; set; } = "Zitei/1.0 (sistema de gestao de infraestrutura urbana)";

    public int TimeoutSegundos { get; set; } = 8;
}

/// <summary>
/// Geocodificação via Nominatim (OpenStreetMap). Respeita a política de uso do serviço público:
/// no máximo 1 requisição por segundo, User-Agent identificado e cache dos resultados.
/// </summary>
public sealed class NominatimGeocodificador : IGeocodificador
{
    // O limite de 1 req/s vale para a aplicação inteira, não por requisição HTTP: estado estático.
    private static readonly SemaphoreSlim Fila = new(1, 1);
    private static DateTime _ultimaChamadaUtc = DateTime.MinValue;
    private static readonly TimeSpan Intervalo = TimeSpan.FromMilliseconds(1100);
    private static readonly TimeSpan EsperaMaximaNaFila = TimeSpan.FromSeconds(6);

    private static readonly TimeSpan CacheAcerto = TimeSpan.FromHours(24);
    private static readonly TimeSpan CacheVazio = TimeSpan.FromMinutes(30);
    private const int MaxResultados = 3;

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NominatimGeocodificador> _logger;

    public NominatimGeocodificador(
        HttpClient http, IMemoryCache cache, ILogger<NominatimGeocodificador> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ResultadoGeocodificacao>> BuscarAsync(ConsultaEndereco consulta, CancellationToken ct = default)
    {
        var consultas = NominatimParser.MontarConsultas(consulta);

        // Do texto mais específico ao mais genérico: usa o primeiro que devolver algo.
        foreach (var q in consultas)
        {
            var chave = "geo:q:" + q.ToLowerInvariant();
            if (!_cache.TryGetValue(chave, out IReadOnlyList<ResultadoGeocodificacao>? resultados) || resultados is null)
            {
                var url = $"search?format=jsonv2&addressdetails=1&limit={MaxResultados}&countrycodes=br&accept-language=pt-BR&q={Uri.EscapeDataString(q)}";
                resultados = NominatimParser.ParseBusca(await ObterAsync(url, ct));
                _cache.Set(chave, resultados, resultados.Count > 0 ? CacheAcerto : CacheVazio);
            }

            if (resultados.Count > 0) return resultados;
        }

        return [];
    }

    public async Task<ResultadoGeocodificacao?> ReversoAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        // ~1 m de precisão: pontos praticamente iguais compartilham o cache.
        var lat = Math.Round(latitude, 5).ToString("F5", CultureInfo.InvariantCulture);
        var lon = Math.Round(longitude, 5).ToString("F5", CultureInfo.InvariantCulture);
        var chave = $"geo:r:{lat},{lon}";

        if (_cache.TryGetValue(chave, out ResultadoGeocodificacao? cacheado))
        {
            return cacheado;
        }

        var url = $"reverse?format=jsonv2&addressdetails=1&zoom=18&accept-language=pt-BR&lat={lat}&lon={lon}";
        var resultado = NominatimParser.ParseReverso(await ObterAsync(url, ct));

        _cache.Set(chave, resultado, resultado is null ? CacheVazio : CacheAcerto);
        return resultado;
    }

    private async Task<string> ObterAsync(string urlRelativa, CancellationToken ct)
    {
        // Espera a vez e garante o intervalo mínimo entre chamadas ao serviço.
        if (!await Fila.WaitAsync(EsperaMaximaNaFila, ct))
        {
            throw new GeocodificacaoIndisponivelException("Muitas consultas de endereço ao mesmo tempo. Tente novamente em instantes.");
        }

        try
        {
            var espera = _ultimaChamadaUtc + Intervalo - DateTime.UtcNow;
            if (espera > TimeSpan.Zero) await Task.Delay(espera, ct);

            try
            {
                using var resposta = await _http.GetAsync(urlRelativa, ct);
                _ultimaChamadaUtc = DateTime.UtcNow;

                if (resposta.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("Nominatim recusou a consulta ({Status}). Verifique o User-Agent e o volume de uso.", (int)resposta.StatusCode);
                    throw new GeocodificacaoIndisponivelException("O serviço de endereços está recusando consultas no momento. Tente novamente mais tarde.");
                }

                if (!resposta.IsSuccessStatusCode)
                {
                    throw new GeocodificacaoIndisponivelException($"O serviço de endereços respondeu com erro {(int)resposta.StatusCode}.");
                }

                return await resposta.Content.ReadAsStringAsync(ct);
            }
            catch (HttpRequestException ex)
            {
                _ultimaChamadaUtc = DateTime.UtcNow;
                throw new GeocodificacaoIndisponivelException("Não foi possível falar com o serviço de endereços.", ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _ultimaChamadaUtc = DateTime.UtcNow;
                throw new GeocodificacaoIndisponivelException("O serviço de endereços demorou demais para responder.", ex);
            }
        }
        finally
        {
            Fila.Release();
        }
    }
}

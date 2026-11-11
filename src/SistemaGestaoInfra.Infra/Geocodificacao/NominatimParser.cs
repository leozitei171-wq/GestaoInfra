using System.Globalization;
using System.Text.Json;
using SistemaGestaoInfra.Application.Common.Interfaces;

namespace SistemaGestaoInfra.Infra.Geocodificacao;

/// <summary>
/// Converte as respostas JSON do Nominatim (formato jsonv2 com addressdetails=1) e monta as consultas.
/// Sem acesso à rede: tudo aqui é determinístico e coberto por testes.
/// </summary>
public static class NominatimParser
{
    /// <summary>Resposta de /search: uma lista. Itens com coordenadas inválidas são descartados.</summary>
    public static IReadOnlyList<ResultadoGeocodificacao> ParseBusca(string json)
    {
        using var doc = Abrir(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new GeocodificacaoIndisponivelException("Resposta inesperada do serviço de endereços.");
        }

        var lista = new List<ResultadoGeocodificacao>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var r = ParseItem(item);
            if (r is not null) lista.Add(r);
        }

        return lista;
    }

    /// <summary>Resposta de /reverse: um objeto, ou {"error": "..."} quando não há endereço no ponto.</summary>
    public static ResultadoGeocodificacao? ParseReverso(string json)
    {
        using var doc = Abrir(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new GeocodificacaoIndisponivelException("Resposta inesperada do serviço de endereços.");
        }

        if (doc.RootElement.TryGetProperty("error", out _))
        {
            return null;
        }

        return ParseItem(doc.RootElement);
    }

    /// <summary>
    /// Consultas em texto livre, da mais específica para a mais genérica, sem repetições.
    /// O chamador usa a primeira que devolver resultado.
    /// </summary>
    public static IReadOnlyList<string> MontarConsultas(ConsultaEndereco c)
    {
        var rua = Limpar(c.Rua);
        var numero = Limpar(c.Numero);
        var bairro = Limpar(c.Bairro);
        var cidade = Limpar(c.Cidade);
        var uf = Limpar(c.Uf);
        var cep = NormalizarCep(c.Cep);

        var consultas = new List<string>();
        void Adicionar(params string?[] partes)
        {
            var q = string.Join(", ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
            if (q.Length > 0 && !consultas.Contains(q, StringComparer.OrdinalIgnoreCase)) consultas.Add(q);
        }

        var ruaComNumero = rua is null ? null : numero is null ? rua : $"{rua}, {numero}";

        if (rua is not null)
        {
            Adicionar(ruaComNumero, bairro, cidade, uf, "Brasil");   // mais específica
            Adicionar(ruaComNumero, cidade, uf, "Brasil");           // sem bairro (cadastros divergem)
            if (numero is not null) Adicionar(rua, cidade, uf, "Brasil");   // sem número: ponto aproximado na via
        }

        if (cep is not null)
        {
            Adicionar(cep, cidade, uf, "Brasil");
        }

        if (rua is null && bairro is not null)
        {
            Adicionar(bairro, cidade, uf, "Brasil");
        }

        return consultas;
    }

    /// <summary>Somente dígitos, no formato 00000-000; nulo se não tiver 8 dígitos.</summary>
    public static string? NormalizarCep(string? cep)
    {
        var d = new string((cep ?? string.Empty).Where(char.IsDigit).ToArray());
        return d.Length == 8 ? $"{d[..5]}-{d[5..]}" : null;
    }

    // ------------------------------------------------------------------ internos

    private static JsonDocument Abrir(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new GeocodificacaoIndisponivelException("Resposta ilegível do serviço de endereços.", ex);
        }
    }

    private static ResultadoGeocodificacao? ParseItem(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object) return null;

        if (!TentarDouble(item, "lat", out var lat) || !TentarDouble(item, "lon", out var lon)) return null;
        if (double.IsNaN(lat) || double.IsNaN(lon) || lat is < -90 or > 90 || lon is < -180 or > 180) return null;

        var end = item.TryGetProperty("address", out var a) && a.ValueKind == JsonValueKind.Object ? a : default;

        var rua = Primeiro(end, "road", "pedestrian", "footway", "path", "residential", "cycleway");
        var numero = Texto(end, "house_number");
        var bairro = Primeiro(end, "suburb", "neighbourhood", "quarter", "city_district", "borough");
        var cidade = Primeiro(end, "city", "town", "village", "municipality");
        var cep = NormalizarCep(Texto(end, "postcode"));
        var uf = ExtrairUf(Texto(end, "ISO3166-2-lvl4"));
        var descricao = Texto(item, "display_name") ?? string.Join(", ", new[] { rua, numero, bairro, cidade, uf }.Where(x => x is not null));

        var precisao = numero is not null ? PrecisaoGeocodificacao.Endereco
            : rua is not null ? PrecisaoGeocodificacao.Via
            : PrecisaoGeocodificacao.Regiao;

        return new ResultadoGeocodificacao(lat, lon, rua, numero, bairro, cidade, uf, cep, descricao, precisao);
    }

    /// <summary>"BR-SP" → "SP".</summary>
    private static string? ExtrairUf(string? iso)
    {
        if (iso is null) return null;
        var i = iso.LastIndexOf('-');
        var uf = i >= 0 ? iso[(i + 1)..] : iso;
        return uf.Length == 2 ? uf.ToUpperInvariant() : null;
    }

    private static bool TentarDouble(JsonElement e, string nome, out double valor)
    {
        valor = 0;
        if (!e.TryGetProperty(nome, out var p)) return false;

        // O Nominatim envia lat/lon como texto ("-21.17"); aceita número também.
        return p.ValueKind switch
        {
            JsonValueKind.String => double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out valor),
            JsonValueKind.Number => p.TryGetDouble(out valor),
            _ => false
        };
    }

    private static string? Texto(JsonElement e, string nome)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(nome, out var p) || p.ValueKind != JsonValueKind.String) return null;
        var t = p.GetString()?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private static string? Primeiro(JsonElement e, params string[] nomes)
    {
        foreach (var n in nomes)
        {
            var t = Texto(e, n);
            if (t is not null) return t;
        }

        return null;
    }

    private static string? Limpar(string? s)
    {
        var t = s?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }
}

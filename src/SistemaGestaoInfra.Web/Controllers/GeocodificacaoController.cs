using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Application.Common.Interfaces;
using SistemaGestaoInfra.Domain.Common;
using SistemaGestaoInfra.Infra.Data;

namespace SistemaGestaoInfra.Web.Controllers;

/// <summary>
/// Ponte autenticada entre o formulário de atividade e o serviço de geocodificação.
/// Cidade e UF vêm SEMPRE da prefeitura do usuário (nunca do navegador), para que o serviço não
/// seja usado como proxy aberto para buscar em qualquer lugar.
/// </summary>
[Authorize(Policy = Politicas.Execucao)]
[EnableRateLimiting(PoliticaLimite)]
public class GeocodificacaoController : Controller
{
    public const string PoliticaLimite = "geocodificacao";

    private const int MaxRua = 255, MaxNumero = 20, MaxBairro = 255, MaxCep = 9;

    private readonly IGeocodificador _geocodificador;
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenant;

    public GeocodificacaoController(IGeocodificador geocodificador, AppDbContext db, ITenantProvider tenant)
    {
        _geocodificador = geocodificador;
        _db = db;
        _tenant = tenant;
    }

    /// <summary>Endereço digitado → possíveis pontos no mapa.</summary>
    [HttpGet]
    public async Task<IActionResult> Buscar(string? rua, string? numero, string? bairro, string? cep, CancellationToken ct)
    {
        rua = Aparar(rua); numero = Aparar(numero); bairro = Aparar(bairro); cep = Aparar(cep);

        if ((rua?.Length ?? 0) > MaxRua || (numero?.Length ?? 0) > MaxNumero ||
            (bairro?.Length ?? 0) > MaxBairro || (cep?.Length ?? 0) > MaxCep)
            return Erro(400, "Algum campo do endereço está longo demais.");

        if (rua is null && bairro is null && cep is null)
            return Erro(400, "Informe ao menos a rua, o bairro ou o CEP.");

        var cidade = await CidadeDaPrefeituraAsync(ct);
        if (cidade is null)
            return Erro(400, "Não foi possível identificar a prefeitura do usuário.");

        try
        {
            var achados = await _geocodificador.BuscarAsync(
                new ConsultaEndereco(rua, numero, bairro, cep, cidade.Value.Cidade, cidade.Value.Uf), ct);

            Response.Headers.CacheControl = "private, no-store";
            return Json(new
            {
                encontrado = achados.Count > 0,
                resultados = achados.Select(r => Mapear(r, cidade.Value))
            });
        }
        catch (GeocodificacaoIndisponivelException ex)
        {
            return Erro(503, ex.Message);
        }
    }

    /// <summary>Coordenadas (ex.: GPS do aparelho) → endereço.</summary>
    [HttpGet]
    public async Task<IActionResult> Reverso(double lat, double lng, CancellationToken ct)
    {
        if (!double.IsFinite(lat) || !double.IsFinite(lng) || lat is < -90 or > 90 || lng is < -180 or > 180)
            return Erro(400, "Coordenadas inválidas.");

        var cidade = await CidadeDaPrefeituraAsync(ct);
        if (cidade is null)
            return Erro(400, "Não foi possível identificar a prefeitura do usuário.");

        try
        {
            var r = await _geocodificador.ReversoAsync(lat, lng, ct);

            Response.Headers.CacheControl = "private, no-store";
            return Json(new
            {
                encontrado = r is not null,
                resultado = r is null ? null : Mapear(r, cidade.Value)
            });
        }
        catch (GeocodificacaoIndisponivelException ex)
        {
            return Erro(503, ex.Message);
        }
    }

    // ------------------------------------------------------------------ internos

    private async Task<(string Cidade, string Uf)?> CidadeDaPrefeituraAsync(CancellationToken ct)
    {
        if (_tenant.PrefeituraId is not int id) return null;

        var p = await _db.Prefeituras.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Cidade, x.Uf })
            .FirstOrDefaultAsync(ct);

        return p is null ? null : (p.Cidade, p.Uf);
    }

    private static object Mapear(ResultadoGeocodificacao r, (string Cidade, string Uf) prefeitura)
    {
        // Só afirma "fora da cidade" quando o serviço informou a cidade/UF e elas divergem da prefeitura.
        var fora = (r.Cidade is not null && !TextoComparavel.Iguais(r.Cidade, prefeitura.Cidade))
                || (r.Uf is not null && !TextoComparavel.Iguais(r.Uf, prefeitura.Uf));

        return new
        {
            lat = r.Latitude,
            lng = r.Longitude,
            rua = r.Rua,
            numero = r.Numero,
            bairro = r.Bairro,
            cidade = r.Cidade,
            uf = r.Uf,
            cep = r.Cep,
            descricao = r.Descricao,
            precisao = r.Precisao.ToString().ToLowerInvariant(),   // endereco | via | regiao
            foraDaCidade = fora
        };
    }

    private JsonResult Erro(int status, string mensagem)
    {
        Response.StatusCode = status;
        return Json(new { erro = mensagem });
    }

    private static string? Aparar(string? s)
    {
        var t = s?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }
}

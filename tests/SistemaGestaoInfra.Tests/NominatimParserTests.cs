using SistemaGestaoInfra.Application.Common.Interfaces;
using SistemaGestaoInfra.Infra.Geocodificacao;

namespace SistemaGestaoInfra.Tests;

public class NominatimParserTests
{
    private const string BuscaComNumero = """
        [{"place_id":1,"lat":"-21.2000123","lon":"-47.8200456","display_name":"123, Rua Teste, Centro, Ribeirão Preto, São Paulo, Brasil",
          "addresstype":"building","address":{"house_number":"123","road":"Rua Teste","suburb":"Centro","city":"Ribeirão Preto",
          "state":"São Paulo","ISO3166-2-lvl4":"BR-SP","postcode":"14015-000","country":"Brasil"}}]
        """;

    [Fact]
    public void Busca_com_numero_extrai_ponto_endereco_e_precisao()
    {
        var r = Assert.Single(NominatimParser.ParseBusca(BuscaComNumero));

        Assert.Equal(-21.2000123, r.Latitude, 7);
        Assert.Equal(-47.8200456, r.Longitude, 7);
        Assert.Equal("Rua Teste", r.Rua);
        Assert.Equal("123", r.Numero);
        Assert.Equal("Centro", r.Bairro);
        Assert.Equal("Ribeirão Preto", r.Cidade);
        Assert.Equal("SP", r.Uf);
        Assert.Equal("14015-000", r.Cep);
        Assert.Equal(PrecisaoGeocodificacao.Endereco, r.Precisao);
    }

    [Fact]
    public void Sem_numero_a_precisao_e_via()
    {
        var json = """[{"lat":"-21.1","lon":"-47.8","display_name":"Rua A","address":{"road":"Rua A","city":"Ribeirão Preto"}}]""";

        Assert.Equal(PrecisaoGeocodificacao.Via, NominatimParser.ParseBusca(json)[0].Precisao);
    }

    [Fact]
    public void Sem_rua_a_precisao_e_regiao()
    {
        var json = """[{"lat":"-21.1","lon":"-47.8","display_name":"Centro","address":{"suburb":"Centro","city":"Ribeirão Preto"}}]""";

        Assert.Equal(PrecisaoGeocodificacao.Regiao, NominatimParser.ParseBusca(json)[0].Precisao);
    }

    [Theory]
    [InlineData("city", "Ribeirão Preto")]
    [InlineData("town", "Sertãozinho")]
    [InlineData("village", "Vila Pequena")]
    [InlineData("municipality", "Município X")]
    public void Cidade_vem_de_qualquer_nivel_administrativo(string campo, string valor)
    {
        var json = $$$"""[{"lat":"-21.1","lon":"-47.8","display_name":"x","address":{"road":"Rua A","{{{campo}}}":"{{{valor}}}"}}]""";

        Assert.Equal(valor, NominatimParser.ParseBusca(json)[0].Cidade);
    }

    [Fact]
    public void Bairro_usa_neighbourhood_quando_nao_ha_suburb()
    {
        var json = """[{"lat":"-21.1","lon":"-47.8","display_name":"x","address":{"road":"Rua A","neighbourhood":"Jardim Tal"}}]""";

        Assert.Equal("Jardim Tal", NominatimParser.ParseBusca(json)[0].Bairro);
    }

    [Fact]
    public void Lista_vazia_quando_nada_foi_encontrado()
    {
        Assert.Empty(NominatimParser.ParseBusca("[]"));
    }

    [Theory]
    [InlineData("""[{"lat":"abc","lon":"-47.8"}]""")]
    [InlineData("""[{"lat":"91","lon":"-47.8"}]""")]
    [InlineData("""[{"lat":"-21.1","lon":"181"}]""")]
    [InlineData("""[{"lat":"-21.1"}]""")]
    [InlineData("""[{"lat":"NaN","lon":"-47.8"}]""")]
    [InlineData("""[1, "texto", null]""")]
    public void Itens_com_coordenadas_invalidas_sao_descartados(string json)
    {
        Assert.Empty(NominatimParser.ParseBusca(json));
    }

    [Fact]
    public void Item_invalido_nao_derruba_os_validos()
    {
        var json = """[{"lat":"x","lon":"y"},{"lat":"-21.1","lon":"-47.8","display_name":"ok","address":{"road":"Rua A"}}]""";

        Assert.Single(NominatimParser.ParseBusca(json));
    }

    [Theory]
    [InlineData("isto não é json")]
    [InlineData("")]
    [InlineData("<html>429 Too Many Requests</html>")]
    [InlineData("""{"nao":"e uma lista"}""")]
    public void Resposta_ilegivel_vira_excecao_de_indisponibilidade(string json)
    {
        Assert.Throws<GeocodificacaoIndisponivelException>(() => NominatimParser.ParseBusca(json));
    }

    [Fact]
    public void Reverso_extrai_o_endereco()
    {
        var json = """
            {"lat":"-21.2","lon":"-47.82","display_name":"Rua Teste, Centro","address":{"road":"Rua Teste","house_number":"45",
             "suburb":"Centro","city":"Ribeirão Preto","ISO3166-2-lvl4":"BR-SP","postcode":"14015000"}}
            """;

        var r = NominatimParser.ParseReverso(json);

        Assert.NotNull(r);
        Assert.Equal("Rua Teste", r!.Rua);
        Assert.Equal("45", r.Numero);
        Assert.Equal("14015-000", r.Cep);   // CEP sem hífen é normalizado
    }

    [Fact]
    public void Reverso_sem_endereco_no_ponto_retorna_nulo()
    {
        Assert.Null(NominatimParser.ParseReverso("""{"error":"Unable to geocode"}"""));
    }

    [Fact]
    public void Reverso_com_lista_em_vez_de_objeto_e_rejeitado()
    {
        Assert.Throws<GeocodificacaoIndisponivelException>(() => NominatimParser.ParseReverso("[]"));
    }

    [Theory]
    [InlineData("BR-SP", "SP")]
    [InlineData("BR-MG", "MG")]
    [InlineData("US-CA", "CA")]
    public void Uf_e_extraida_do_codigo_iso(string iso, string esperado)
    {
        var json = $$$"""[{"lat":"-21.1","lon":"-47.8","display_name":"x","address":{"road":"R","ISO3166-2-lvl4":"{{{iso}}}"}}]""";

        Assert.Equal(esperado, NominatimParser.ParseBusca(json)[0].Uf);
    }

    [Theory]
    [InlineData("14031595", "14031-595")]
    [InlineData("14031-595", "14031-595")]
    [InlineData(" 14.031-595 ", "14031-595")]
    [InlineData("1403", null)]
    [InlineData("abc", null)]
    [InlineData(null, null)]
    public void Cep_e_normalizado_ou_descartado(string? entrada, string? esperado)
    {
        Assert.Equal(esperado, NominatimParser.NormalizarCep(entrada));
    }

    [Fact]
    public void Consultas_vao_da_mais_especifica_para_a_mais_generica()
    {
        var q = NominatimParser.MontarConsultas(new ConsultaEndereco("Rua Teste", "123", "Centro", null, "Ribeirão Preto", "SP"));

        Assert.Equal(
        [
            "Rua Teste, 123, Centro, Ribeirão Preto, SP, Brasil",
            "Rua Teste, 123, Ribeirão Preto, SP, Brasil",
            "Rua Teste, Ribeirão Preto, SP, Brasil"
        ], q);
    }

    [Fact]
    public void Sem_numero_nao_repete_consultas()
    {
        var q = NominatimParser.MontarConsultas(new ConsultaEndereco("Rua Teste", null, "Centro", null, "Ribeirão Preto", "SP"));

        Assert.Equal(
        [
            "Rua Teste, Centro, Ribeirão Preto, SP, Brasil",
            "Rua Teste, Ribeirão Preto, SP, Brasil"
        ], q);
    }

    [Fact]
    public void So_com_cep_consulta_pelo_cep()
    {
        var q = NominatimParser.MontarConsultas(new ConsultaEndereco(null, null, null, "14031595", "Ribeirão Preto", "SP"));

        Assert.Equal(["14031-595, Ribeirão Preto, SP, Brasil"], q);
    }

    [Fact]
    public void So_com_bairro_consulta_o_bairro()
    {
        var q = NominatimParser.MontarConsultas(new ConsultaEndereco(null, null, "Centro", null, "Ribeirão Preto", "SP"));

        Assert.Equal(["Centro, Ribeirão Preto, SP, Brasil"], q);
    }

    [Fact]
    public void Sem_nenhum_dado_nao_gera_consulta()
    {
        Assert.Empty(NominatimParser.MontarConsultas(new ConsultaEndereco(" ", null, "", null, "Ribeirão Preto", "SP")));
    }
}

using SistemaGestaoInfra.Domain.Common;

namespace SistemaGestaoInfra.Tests;

public class CalculoRateioTests
{
    [Fact]
    public void Divide_100_em_3_partes_iguais_sem_perder_centavo()
    {
        var r = CalculoRateio.Dividir(100m, [1m, 1m, 1m]);

        Assert.Equal(100m, r.Sum());
        Assert.Equal([33.34m, 33.33m, 33.33m], r);   // o centavo sobrante vai para o primeiro (desempate estável)
    }

    [Fact]
    public void Respeita_a_proporcao_dos_pesos()
    {
        var r = CalculoRateio.Dividir(1000m, [1m, 3m]);

        Assert.Equal([250m, 750m], r);
    }

    [Fact]
    public void Peso_zero_recebe_zero()
    {
        var r = CalculoRateio.Dividir(100m, [0m, 5m, 5m]);

        Assert.Equal(0m, r[0]);
        Assert.Equal(100m, r.Sum());
    }

    [Fact]
    public void Um_unico_destinatario_recebe_tudo()
    {
        Assert.Equal([123.45m], CalculoRateio.Dividir(123.45m, [7m]));
    }

    [Fact]
    public void Valor_com_mais_de_duas_casas_e_arredondado_para_centavos()
    {
        var r = CalculoRateio.Dividir(10.005m, [1m, 1m]);

        Assert.Equal(10.01m, r.Sum());   // 10,005 -> 10,01 (afasta do zero)
    }

    [Fact]
    public void Mantem_a_ordem_dos_destinatarios()
    {
        var r = CalculoRateio.Dividir(90m, [10m, 20m, 60m]);

        Assert.Equal([10m, 20m, 60m], r);
    }

    [Fact]
    public void Valor_zero_resulta_em_zeros()
    {
        Assert.All(CalculoRateio.Dividir(0m, [1m, 2m, 3m]), v => Assert.Equal(0m, v));
    }

    [Fact]
    public void Rejeita_entradas_invalidas()
    {
        Assert.Throws<ArgumentException>(() => CalculoRateio.Dividir(100m, []));
        Assert.Throws<ArgumentException>(() => CalculoRateio.Dividir(100m, [0m, 0m]));
        Assert.Throws<ArgumentException>(() => CalculoRateio.Dividir(100m, [1m, -1m]));
    }

    [Fact]
    public void A_soma_e_sempre_exata_em_milhares_de_entradas_aleatorias()
    {
        var rng = new Random(20261007);   // semente fixa: o teste é determinístico
        for (var n = 0; n < 5000; n++)
        {
            var total = Math.Round((decimal)(rng.NextDouble() * 1_000_000), 2);
            var qtd = rng.Next(1, 40);
            var pesos = Enumerable.Range(0, qtd)
                .Select(_ => Math.Round((decimal)(rng.NextDouble() * 100_000), 2))
                .ToList();
            if (pesos.Sum() == 0) pesos[0] = 1m;

            var r = CalculoRateio.Dividir(total, pesos);

            Assert.Equal(total, r.Sum());                       // nenhum centavo criado ou perdido
            Assert.All(r, v => Assert.True(v >= 0));
            Assert.All(r, v => Assert.Equal(v, Math.Round(v, 2)));   // sempre em centavos
        }
    }

    [Fact]
    public void Cada_parcela_difere_do_valor_exato_em_menos_de_um_centavo()
    {
        var pesos = new List<decimal> { 3m, 7m, 11m, 19m, 41m };
        var total = 1234.57m;
        var soma = pesos.Sum();

        var r = CalculoRateio.Dividir(total, pesos);

        for (var i = 0; i < pesos.Count; i++)
        {
            var exato = total * pesos[i] / soma;
            Assert.True(Math.Abs(r[i] - exato) < 0.01m, $"parcela {i}: {r[i]} vs {exato}");
        }
    }
}

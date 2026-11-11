using System.Diagnostics;
using SistemaGestaoInfra.Domain.Common;

namespace SistemaGestaoInfra.Tests;

public class AnonimizadorTests
{
    [Theory]
    [InlineData("Falar com joao.silva@exemplo.com.br sobre o buraco", "[e-mail oculto]")]
    [InlineData("CPF do morador 123.456.789-09 na rua", "[CPF oculto]")]
    [InlineData("CPF sem pontos 12345678909 ok", "[CPF oculto]")]
    [InlineData("Ligar (16) 99999-9999 para confirmar", "[telefone oculto]")]
    [InlineData("tel 16 3333-4444", "[telefone oculto]")]
    [InlineData("whatsapp +55 16 99999 9999", "[telefone oculto]")]
    [InlineData("contato 99999-9999", "[telefone oculto]")]
    public void Oculta_dados_pessoais_conhecidos(string entrada, string marcador)
    {
        var saida = Anonimizador.Mascarar(entrada);

        Assert.Contains(marcador, saida);
    }

    [Theory]
    [InlineData("joao.silva@exemplo.com.br")]
    [InlineData("123.456.789-09")]
    [InlineData("99999-9999")]
    public void O_valor_original_nao_sobrevive(string sensivel)
    {
        var saida = Anonimizador.Mascarar($"antes {sensivel} depois");

        Assert.DoesNotContain(sensivel, saida);
        Assert.StartsWith("antes ", saida);
        Assert.EndsWith(" depois", saida);
    }

    [Theory]
    [InlineData("Mato alto no canteiro central da avenida")]
    [InlineData("Buraco de 30 cm na Rua 7 de Setembro, 120")]
    [InlineData("Poda de árvore em 2026, protocolo 45")]
    public void Nao_altera_texto_comum(string texto)
    {
        Assert.Equal(texto, Anonimizador.Mascarar(texto));
    }

    [Fact]
    public void Entrada_nula_ou_vazia_vira_texto_vazio()
    {
        Assert.Equal(string.Empty, Anonimizador.Mascarar(null));
        Assert.Equal(string.Empty, Anonimizador.Mascarar(""));
    }

    [Fact]
    public void Resumir_mascara_antes_de_cortar()
    {
        // Cortar primeiro deixaria metade do CPF visível.
        var texto = new string('x', 10) + " 123.456.789-09 " + new string('y', 50);

        var r = Anonimizador.Resumir(texto, 20);

        Assert.DoesNotContain("123.456", r);
        Assert.EndsWith("…", r);
    }

    [Fact]
    public void Entrada_hostil_nao_trava_o_servidor()
    {
        var hostil = new string('9', 200_000) + "@" + new string('a', 200_000);

        var cronometro = Stopwatch.StartNew();
        var _ = Anonimizador.Mascarar(hostil);
        cronometro.Stop();

        Assert.True(cronometro.ElapsedMilliseconds < 3000, $"demorou {cronometro.ElapsedMilliseconds} ms");
    }
}

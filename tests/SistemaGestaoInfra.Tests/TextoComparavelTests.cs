using SistemaGestaoInfra.Domain.Common;

namespace SistemaGestaoInfra.Tests;

public class TextoComparavelTests
{
    [Theory]
    [InlineData("Ribeirão Preto", "RIBEIRAO PRETO")]
    [InlineData("  ribeirão   preto ", "Ribeirao Preto")]
    [InlineData("São José do Rio Preto", "sao jose do rio preto")]
    [InlineData("Ribeirão-Preto", "Ribeirão Preto")]
    [InlineData("SP", "sp")]
    public void Ignora_acentos_caixa_e_espacos(string a, string b)
    {
        Assert.True(TextoComparavel.Iguais(a, b));
    }

    [Theory]
    [InlineData("Ribeirão Preto", "Sertãozinho")]
    [InlineData("SP", "MG")]
    [InlineData("Campinas", "Campina Grande")]
    public void Textos_diferentes_nao_sao_iguais(string a, string b)
    {
        Assert.False(TextoComparavel.Iguais(a, b));
    }

    [Theory]
    [InlineData(null, "SP")]
    [InlineData("SP", null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void Vazio_ou_nulo_nunca_e_igual(string? a, string? b)
    {
        // Evita afirmar "mesma cidade" sem ter a informação.
        Assert.False(TextoComparavel.Iguais(a, b));
    }
}

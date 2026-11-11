namespace SistemaGestaoInfra.Domain.Common;

/// <summary>
/// Divisão proporcional de um valor em reais, sem perder nem criar centavos.
/// Usa o método do maior resto: cada parcela é truncada em centavos e os centavos que sobram
/// são entregues, um a um, às parcelas com maior parte fracionária perdida.
/// </summary>
public static class CalculoRateio
{
    /// <param name="valorTotal">Valor a dividir (será arredondado para centavos).</param>
    /// <param name="pesos">Peso de cada destinatário; devem ser &gt;= 0 e a soma &gt; 0.</param>
    /// <returns>Valores na mesma ordem dos pesos; a soma é exatamente <paramref name="valorTotal"/>.</returns>
    public static IReadOnlyList<decimal> Dividir(decimal valorTotal, IReadOnlyList<decimal> pesos)
    {
        ArgumentNullException.ThrowIfNull(pesos);
        if (pesos.Count == 0) throw new ArgumentException("Informe ao menos um destinatário.", nameof(pesos));
        if (pesos.Any(p => p < 0)) throw new ArgumentException("Os pesos não podem ser negativos.", nameof(pesos));

        var somaPesos = pesos.Sum();
        if (somaPesos <= 0) throw new ArgumentException("A soma dos pesos deve ser maior que zero.", nameof(pesos));

        // Trabalha em centavos inteiros para eliminar erro de ponto flutuante.
        var totalCentavos = (long)Math.Round(valorTotal * 100m, MidpointRounding.AwayFromZero);
        var sinal = totalCentavos < 0 ? -1 : 1;
        var alvo = Math.Abs(totalCentavos);

        var parcelas = new long[pesos.Count];
        var restos = new decimal[pesos.Count];
        long distribuido = 0;

        for (var i = 0; i < pesos.Count; i++)
        {
            var exato = alvo * pesos[i] / somaPesos;
            parcelas[i] = (long)Math.Floor(exato);
            restos[i] = exato - parcelas[i];
            distribuido += parcelas[i];
        }

        // Centavos que sobraram: um para cada maior resto (desempate pela ordem original = determinístico).
        var faltam = alvo - distribuido;
        foreach (var i in Enumerable.Range(0, pesos.Count)
                     .OrderByDescending(i => restos[i]).ThenBy(i => i)
                     .Take((int)faltam))
        {
            parcelas[i]++;
        }

        return parcelas.Select(c => sinal * c / 100m).ToList();
    }
}

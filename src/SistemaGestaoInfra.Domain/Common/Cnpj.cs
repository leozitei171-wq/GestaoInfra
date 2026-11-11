namespace SistemaGestaoInfra.Domain.Common;

/// <summary>Normalização, validação e formatação de CNPJ.</summary>
public static class Cnpj
{
    /// <summary>Remove tudo que não for dígito.</summary>
    public static string Normalizar(string? valor) =>
        new((valor ?? string.Empty).Where(char.IsDigit).ToArray());

    public static bool Valido(string? valor)
    {
        var d = Normalizar(valor);
        if (d.Length != 14 || d.Distinct().Count() == 1)
        {
            return false; // tamanho errado ou sequência repetida (00000000000000...)
        }

        return Digito(d, 12) == d[12] - '0' && Digito(d, 13) == d[13] - '0';
    }

    public static string Formatar(string? valor)
    {
        var d = Normalizar(valor);
        return d.Length != 14
            ? d
            : $"{d[..2]}.{d.Substring(2, 3)}.{d.Substring(5, 3)}/{d.Substring(8, 4)}-{d.Substring(12, 2)}";
    }

    private static int Digito(string d, int tamanho)
    {
        int[] pesos = tamanho == 12
            ? [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]
            : [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        var soma = 0;
        for (var i = 0; i < tamanho; i++)
        {
            soma += (d[i] - '0') * pesos[i];
        }

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}

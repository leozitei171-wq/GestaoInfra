namespace SistemaGestaoInfra.Domain.Common;

/// <summary>Níveis de alerta de contrato e os limiares que os disparam.</summary>
public static class NivelAlerta
{
    public const string Atencao = "atencao";
    public const string Critico = "critico";
    public const string Vencimento = "vencimento";

    /// <summary>Uso a partir do qual o contrato entra em "atenção" (%).</summary>
    public const decimal LimiteAtencao = 80m;

    /// <summary>Uso a partir do qual o contrato é "crítico" (%).</summary>
    public const decimal LimiteCritico = 100m;

    /// <summary>Dias antes do fim da vigência para avisar o vencimento.</summary>
    public const int DiasAvisoVencimento = 30;

    public static string Rotulo(string nivel) => nivel switch
    {
        Atencao => "Atenção",
        Critico => "Crítico",
        Vencimento => "Vencimento",
        _ => nivel
    };

    /// <summary>Nível de uso para um percentual, ou nulo se abaixo do limiar de atenção.</summary>
    public static string? PorUso(decimal percentual) =>
        percentual >= LimiteCritico ? Critico
        : percentual >= LimiteAtencao ? Atencao
        : null;
}

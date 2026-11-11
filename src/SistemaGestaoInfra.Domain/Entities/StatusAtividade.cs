using SistemaGestaoInfra.Domain.Common;

namespace SistemaGestaoInfra.Domain.Entities;

/// <summary>
/// Status/situação configurável de uma atividade, com cor para exibição no mapa e listagens.
/// </summary>
public class StatusAtividade : EntidadeTenant
{
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    /// <summary>Cor hexadecimal para marcadores do mapa (ex: #AE6BFF).</summary>
    public string Cor { get; set; } = "#8592a3";
    public bool Ativo { get; set; } = true;

    // Navegação
    public Prefeitura? Prefeitura { get; set; }
}

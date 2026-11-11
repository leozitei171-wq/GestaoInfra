using SistemaGestaoInfra.Domain.Common;

namespace SistemaGestaoInfra.Domain.Entities;

/// <summary>
/// Prioridade de uma atividade (ex: URG — Urgente, NORM — Normal).
/// </summary>
public class Prioridade : EntidadeTenant
{
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;

    // Navegação
    public Prefeitura? Prefeitura { get; set; }
}

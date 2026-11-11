using SistemaGestaoInfra.Domain.Common;

namespace SistemaGestaoInfra.Domain.Entities;

/// <summary>
/// Tipo de atividade (ex: CEC — Capina em canteiros, TB — Tapa buraco).
/// Vinculado a um setor responsável.
/// </summary>
public class TipoAtividade : EntidadeTenant
{
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public int? SetorId { get; set; }
    public bool Ativo { get; set; } = true;

    // Navegação
    public Prefeitura? Prefeitura { get; set; }
    public Setor? Setor { get; set; }
}

using SistemaGestaoInfra.Domain.Common;

namespace SistemaGestaoInfra.Domain.Entities;

/// <summary>
/// Setor/Departamento responsável por atividades (ex: INFRA_2132_SLU).
/// </summary>
public class Setor : EntidadeTenant
{
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;

    // Navegação
    public Prefeitura? Prefeitura { get; set; }
    public ICollection<TipoAtividade> TiposAtividade { get; set; } = new List<TipoAtividade>();
}

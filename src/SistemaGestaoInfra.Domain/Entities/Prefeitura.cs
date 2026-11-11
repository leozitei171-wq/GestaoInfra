using SistemaGestaoInfra.Domain.Common;

namespace SistemaGestaoInfra.Domain.Entities;

/// <summary>
/// Prefeitura — representa o tenant do sistema. Todas as entidades operacionais
/// pertencem a uma prefeitura e são isoladas por ela.
/// </summary>
public class Prefeitura : EntidadeBase
{
    public string Nome { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Uf { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;

    // Navegação
    public ICollection<Setor> Setores { get; set; } = new List<Setor>();
    public ICollection<Atividade> Atividades { get; set; } = new List<Atividade>();
}

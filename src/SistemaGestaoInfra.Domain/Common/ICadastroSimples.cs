namespace SistemaGestaoInfra.Domain.Common;

/// <summary>Cadastro simples de código + descrição + situação, reaproveitado por CRUD genérico.</summary>
public interface ICadastroSimples
{
    int Id { get; }
    string Codigo { get; set; }
    string Descricao { get; set; }
    bool Ativo { get; set; }
}

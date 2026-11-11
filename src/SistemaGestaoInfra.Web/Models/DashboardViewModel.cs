namespace SistemaGestaoInfra.Web.Models;

public class DashboardViewModel
{
    public int TotalAtividades { get; set; }
    public int Abertas { get; set; }
    public int EmAndamento { get; set; }
    public int Atrasadas { get; set; }
    public int Concluidas { get; set; }

    public IReadOnlyList<ContagemStatus> PorStatus { get; set; } = [];
    public IReadOnlyList<AtividadeRecente> Recentes { get; set; } = [];

    public record ContagemStatus(string Codigo, string Descricao, string Cor, int Total);

    public record AtividadeRecente(
        int Id,
        string Descricao,
        string Tipo,
        string Status,
        string CorStatus,
        DateTime CriadoEm);
}

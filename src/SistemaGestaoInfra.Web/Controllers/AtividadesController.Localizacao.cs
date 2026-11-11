using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaGestaoInfra.Web.Services;

namespace SistemaGestaoInfra.Web.Controllers;

/// <summary>Definição/correção da localização (coordenadas) de uma atividade já cadastrada.</summary>
public partial class AtividadesController
{
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.Execucao)]
    public async Task<IActionResult> DefinirLocalizacao(
        int id,
        [ModelBinder(typeof(DecimalFlexivelModelBinder))] decimal latitude,
        [ModelBinder(typeof(DecimalFlexivelModelBinder))] decimal longitude,
        CancellationToken ct)
    {
        // A atividade é buscada pelo filtro multi-tenant: id de outra prefeitura => 404.
        var a = await CarregarAsync(id, ct);
        if (a is null) return NotFound();

        if (!ModelState.IsValid)
            return Recusar(id, "Coordenadas inválidas. Clique no mapa para marcar a localização.");

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return Recusar(id, "Coordenadas fora da faixa válida (latitude de -90 a 90, longitude de -180 a 180).");

        // Mesma precisão da coluna (7 casas ≈ 1 cm).
        a.Latitude = Math.Round(latitude, 7);
        a.Longitude = Math.Round(longitude, 7);
        await _db.SaveChangesAsync(ct);   // fica registrado na auditoria (de/para)

        TempData["Sucesso"] = $"Localização da atividade #{id} salva. Ela já aparece no mapa.";
        return RedirectToAction(nameof(Detalhe), new { id });
    }
}

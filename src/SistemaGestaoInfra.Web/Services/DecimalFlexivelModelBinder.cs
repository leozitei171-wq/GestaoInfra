using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SistemaGestaoInfra.Web.Services;

/// <summary>
/// Converte decimais aceitando ponto ou vírgula como separador, independente da cultura do servidor.
/// Usado em coordenadas (o mapa grava com ponto) e em valores monetários/quantidades digitados em pt-BR.
/// Também aceita separador de milhar quando há ambos: "1.234,56" e "1,234.56".
/// </summary>
public class DecimalFlexivelModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var resultado = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (resultado == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, resultado);

        var texto = resultado.FirstValue?.Trim();
        if (string.IsNullOrEmpty(texto))
        {
            if (bindingContext.ModelMetadata.IsNullableValueType)
            {
                bindingContext.Result = ModelBindingResult.Success(null);
            }
            else
            {
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Informe um valor.");
            }

            return Task.CompletedTask;
        }

        if (TentarConverter(texto, out var valor))
        {
            bindingContext.Result = ModelBindingResult.Success(valor);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName, "Informe um número válido (ex: 1234,56).");
        }

        return Task.CompletedTask;
    }

    /// <summary>Normaliza "1.234,56", "1,234.56", "1234,56" e "1234.56" para decimal.</summary>
    internal static bool TentarConverter(string texto, out decimal valor)
    {
        var ultimoPonto = texto.LastIndexOf('.');
        var ultimaVirgula = texto.LastIndexOf(',');

        string normalizado;
        if (ultimoPonto >= 0 && ultimaVirgula >= 0)
        {
            // O separador que aparece por último é o decimal; o outro é milhar.
            normalizado = ultimaVirgula > ultimoPonto
                ? texto.Replace(".", "").Replace(',', '.')
                : texto.Replace(",", "");
        }
        else
        {
            normalizado = texto.Replace(',', '.');
        }

        return decimal.TryParse(normalizado, NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
    }
}

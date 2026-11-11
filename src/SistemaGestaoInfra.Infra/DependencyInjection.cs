using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SistemaGestaoInfra.Application.Common.Interfaces;
using SistemaGestaoInfra.Infra.Data;
using SistemaGestaoInfra.Infra.Geocodificacao;
using SistemaGestaoInfra.Infra.Storage;

namespace SistemaGestaoInfra.Infra;

/// <summary>
/// Registro dos serviços da camada de infraestrutura.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfraestrutura(
        this IServiceCollection services,
        IConfiguration configuration,
        string raizUploads)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' não configurada.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.Configure<LocalFileStorageOptions>(options =>
        {
            options.RaizFisica = raizUploads;
            options.PrefixoUrl = "/uploads";
        });

        services.AddScoped<IFileStorage, LocalFileStorage>();

        // Geocodificação (endereço <-> coordenadas). Configurável em "Geocodificacao" no appsettings/ambiente.
        services.Configure<NominatimOptions>(configuration.GetSection(NominatimOptions.Secao));
        services.AddMemoryCache();
        services.AddHttpClient<IGeocodificador, NominatimGeocodificador>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<NominatimOptions>>().Value;
            http.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(Math.Clamp(o.TimeoutSegundos, 2, 30));
            http.MaxResponseContentBufferSize = 1024 * 1024;   // respostas legítimas têm poucos KB
            http.DefaultRequestHeaders.UserAgent.ParseAdd(o.UserAgent);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        return services;
    }
}

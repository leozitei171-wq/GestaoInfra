using SistemaGestaoInfra.Infra.Data;

namespace SistemaGestaoInfra.Web.Services;

/// <summary>
/// Executa o motor de atraso ao iniciar a aplicação e depois a cada intervalo.
/// Intervalo configurável em MotorAtraso:IntervaloSegundos (padrão 300 = 5 minutos).
/// </summary>
public class MotorAtrasoService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MotorAtrasoService> _logger;
    private readonly TimeSpan _intervalo;

    public MotorAtrasoService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<MotorAtrasoService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var segundos = configuration.GetValue("MotorAtraso:IntervaloSegundos", 300);
        _intervalo = TimeSpan.FromSeconds(Math.Max(1, segundos));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_intervalo);

        do
        {
            await ExecutarUmaVezAsync(stoppingToken);
        }
        while (await ProximoTickAsync(timer, stoppingToken));
    }

    private async Task ExecutarUmaVezAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var hoje = DateOnly.FromDateTime(DateTime.Now);
            var marcadas = await MotorAtraso.ExecutarAsync(db, hoje, ct);
            if (marcadas > 0)
            {
                _logger.LogInformation("Motor de atraso: {Total} atividade(s) marcada(s) como atrasada(s).", marcadas);
            }

            db.ChangeTracker.Clear();
            var alertas = await AlertasContrato.AtualizarAsync(db, null, hoje, ct);
            if (alertas > 0)
            {
                _logger.LogInformation("Alertas de contrato: {Total} novo(s) alerta(s).", alertas);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // encerramento normal da aplicação
        }
        catch (Exception ex)
        {
            // Uma falha pontual (ex.: banco indisponível) não pode derrubar o serviço; tenta no próximo ciclo.
            _logger.LogError(ex, "Falha ao executar o motor de atraso.");
        }
    }

    private static async Task<bool> ProximoTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

using CondoLink.Api.Features.Observability;

namespace CondoLink.Api.Features.Requests;

public sealed class RequestClosureWorker(IServiceScopeFactory scopes, OperationalTelemetry telemetry, ILogger<RequestClosureWorker> logger) : BackgroundService
{
    internal static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(2);
    internal const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollingInterval);
        do
        {
            try
            {
                await telemetry.RecordWorkerAsync(nameof(RequestClosureWorker), true, PollingInterval, "started", ct: stoppingToken);
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<RequestClosureService>();
                var count = await service.ExpireBatchAsync(DateTime.UtcNow, BatchSize, stoppingToken);
                if (count > 0) logger.LogInformation("Automatically finalized {Count} requests.", count);
                await telemetry.RecordWorkerAsync(nameof(RequestClosureWorker), true, PollingInterval, "completed", true, count, ct: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Request closure expiration cycle failed."); await telemetry.RecordWorkerAsync(nameof(RequestClosureWorker), true, PollingInterval, "completed", false, failures: 1, code: "cycle_failed", ct: CancellationToken.None); await telemetry.EventAsync("Workers", "RequestClosure", "Error", "cycle_failed", ct: CancellationToken.None); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

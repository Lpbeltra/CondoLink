namespace CondoLink.Api.Features.Requests;

public sealed class RequestClosureWorker(IServiceScopeFactory scopes, ILogger<RequestClosureWorker> logger) : BackgroundService
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
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<RequestClosureService>();
                var count = await service.ExpireBatchAsync(DateTime.UtcNow, BatchSize, stoppingToken);
                if (count > 0) logger.LogInformation("Automatically finalized {Count} requests.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Request closure expiration cycle failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

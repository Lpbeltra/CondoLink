namespace CondoLink.Api.Features.WebPush;

public sealed class WebPushWorker(
    IWebPushQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<WebPushWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var notificationId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<WebPushDispatcher>()
                    .DispatchAsync(notificationId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Web Push job failed. NotificationId: {NotificationId}.", notificationId);
            }
        }
    }
}

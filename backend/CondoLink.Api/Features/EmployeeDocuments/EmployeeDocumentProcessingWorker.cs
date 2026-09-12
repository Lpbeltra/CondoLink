using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeDocuments;

public sealed class EmployeeDocumentProcessingOptions
{
    public const string SectionName = "EmployeeDocumentProcessing";
    public int PollingSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 3;
}

/// <summary>
/// Polls for uploaded batches and runs them through <see cref="EmployeeDocumentProcessingService"/>
/// off the HTTP request thread — splitting/OCR/identification of several
/// multi-page PDFs is not something a request should wait minutes on.
/// </summary>
public sealed class EmployeeDocumentProcessingWorker(
    IServiceScopeFactory scopeFactory,
    Microsoft.Extensions.Options.IOptions<EmployeeDocumentProcessingOptions> options,
    ILogger<EmployeeDocumentProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = TimeSpan.FromSeconds(Math.Clamp(options.Value.PollingSeconds, 2, 60));
            try { await ProcessBatchAsync(stoppingToken); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Employee document processing worker batch failed.");
            }
            await Task.Delay(interval, stoppingToken);
        }
    }

    internal async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<EmployeeDocumentProcessingService>();

        // A crash/restart while a batch was mid-processing would otherwise leave
        // it stuck in Processing forever, since the query below only looks at
        // Uploaded. Nothing is ever persisted until a pass fully succeeds, so
        // resetting back to Uploaded and letting it run again is always safe.
        var now = DateTime.UtcNow;
        var interrupted = await db.EmployeeDocumentBatches
            .Where(x => x.Status == EmployeeDocumentBatchStatus.Processing && x.CreatedAt < now.AddMinutes(-10))
            .ToArrayAsync(ct);
        foreach (var batch in interrupted) batch.RecoverInterruptedProcessing();
        if (interrupted.Length > 0) await db.SaveChangesAsync(ct);

        var batches = await db.EmployeeDocumentBatches
            .Where(x => x.Status == EmployeeDocumentBatchStatus.Uploaded)
            .OrderBy(x => x.CreatedAt)
            .Take(Math.Clamp(options.Value.BatchSize, 1, 10))
            .ToArrayAsync(ct);
        foreach (var batch in batches) await processor.ProcessAsync(batch, ct);
        return batches.Length;
    }
}

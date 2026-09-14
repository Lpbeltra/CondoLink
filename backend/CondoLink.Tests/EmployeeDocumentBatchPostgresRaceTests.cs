using System.Data.Common;
using System.Net;
using CondoLink.Api.Features.EmployeeDocuments;
using CondoLink.Api.Features.EmployeeManagement;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

/// <summary>Real PostgreSQL races; set COMVY_TEST_POSTGRES to a migrated disposable database.</summary>
public sealed class EmployeeDocumentBatchPostgresRaceTests
{
    private static readonly string? Connection = Environment.GetEnvironmentVariable("COMVY_TEST_POSTGRES");

    [Theory]
    [InlineData(false, false)] // distribute commits; delete sees Pending
    [InlineData(false, true)]  // delete commits; distribute sees deleted batch
    [InlineData(true, false)]  // retry commits; delete sees Pending
    [InlineData(true, true)]   // delete commits; retry sees deleted batch
    public async Task Batch_operations_serialize_at_the_same_database_lock(bool retry, bool deleteFirst)
    {
        if (Connection is null) return;
        var pause = new FirstBatchLockPause();
        await using var host = await CoreEndpointTestHost.StartAsync(
            app => app.MapEmployeeDocumentEndpoints(),
            builder =>
            {
                builder.Configuration["FileStorage:RootPath"] = Path.Combine(Path.GetTempPath(), "condolink-race-tests", Guid.NewGuid().ToString("N"));
                builder.Services.AddSingleton<LocalFileStorage>();
                builder.Services.AddScoped<EmployeeManagementAccessService>();
                builder.Services.AddScoped<EmployeeDocumentDistributionService>();
                builder.Services.Configure<WhatsAppOptions>(_ => { });
            }, Connection, pause);

        var seed = await SeedAsync(host, retry);
        using var firstClient = host.ClientFor(seed.UserId);
        using var secondClient = host.ClientFor(seed.UserId);
        var deleteUrl = $"/administrator/employees/documents/batches/{seed.BatchId}";
        var sendUrl = retry
            ? $"{deleteUrl}/documents/{seed.DocumentId}/resend"
            : $"{deleteUrl}/distribute";

        Task<HttpResponseMessage> Send(HttpClient client) => client.PostAsync(sendUrl, null);
        Task<HttpResponseMessage> Delete(HttpClient client) => client.DeleteAsync(deleteUrl);

        var first = deleteFirst ? Delete(firstClient) : Send(firstClient);
        await pause.Acquired.WaitAsync(TimeSpan.FromSeconds(20));
        var second = deleteFirst ? Send(secondClient) : Delete(secondClient);
        try
        {
            // The second request must wait for the first transaction's lock.
            await Task.Delay(150);
            Assert.False(second.IsCompleted);
        }
        finally { pause.Release(); }

        using var firstResponse = await first.WaitAsync(TimeSpan.FromSeconds(20));
        using var secondResponse = await second.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(deleteFirst ? HttpStatusCode.NoContent : retry ? HttpStatusCode.NoContent : HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        var state = await host.WithDbAsync(async db => new
        {
            Batch = await db.EmployeeDocumentBatches.AsNoTracking().SingleAsync(x => x.Id == seed.BatchId),
            Document = await db.EmployeeDocuments.AsNoTracking().SingleAsync(x => x.Id == seed.DocumentId),
            Outbounds = await db.WhatsAppOutboundMessages.AsNoTracking().Where(x => x.EmployeeDocumentId == seed.DocumentId).ToArrayAsync()
        });
        Assert.Equal(deleteFirst, state.Batch.DeletedAt is not null);
        Assert.Equal(deleteFirst, state.Document.DeletedAt is not null);
        Assert.Equal(retry ? (deleteFirst ? 1 : 2) : (deleteFirst ? 0 : 1), state.Outbounds.Length);
        if (!deleteFirst) Assert.Contains(state.Outbounds, x => x.Status == WhatsAppOutboundStatus.Pending);
    }

    private static async Task<(Guid BatchId, Guid DocumentId, Guid UserId)> SeedAsync(CoreEndpointTestHost host, bool retry)
    {
        return await host.WithDbAsync(async db =>
        {
            var company = new ManagementCompany("Race test", null, null, null, null);
            var user = CoreTestSeed.User("Race actor", $"race-{Guid.NewGuid():N}@test.local");
            var member = new ManagementCompanyEmployee(company.Id, user.Id, "Payroll");
            var condominium = new Condominium("Race condominium", null, null);
            condominium.SetManagementCompany(company.Id);
            var employee = new Employee(condominium.Id, "Race employee", null, null, "11999990001", null, null, null);
            var batch = new EmployeeDocumentBatch(null, company.Id, EmployeeDocumentType.Payslip, 8, 2026, user.Id, DateTime.UtcNow);
            batch.StartProcessing(); batch.MarkReadyForReview(); batch.Confirm(user.Id, DateTime.UtcNow);
            var document = new EmployeeDocument(condominium.Id, batch.Id, EmployeeDocumentType.Payslip, 8, 2026,
                "pending", "test.pdf", 1, 1, new string('A', 64), DateTime.UtcNow);
            document.ApplyAutomaticIdentification(employee.Id, EmployeeDocumentIdentificationConfidence.High,
                EmployeeDocumentIdentificationMethod.Cpf, DateTime.UtcNow);
            document.Confirm(DateTime.UtcNow);
            db.AddRange(company, user, member, condominium, employee, batch, document,
                new CondominiumManagementCompanyLink(condominium.Id, company.Id),
                new ManagementCompanyModule(company.Id, ManagementCompanyModuleType.EmployeeManagement, true, DateTime.UtcNow),
                new ManagementCompanyEmployeeModuleGrant(member.Id, ManagementCompanyModuleType.EmployeeManagement, user.Id, DateTime.UtcNow),
                new EmployeeDocumentBatchEmployee(batch.Id, employee.Id));
            await db.SaveChangesAsync();
            if (retry)
            {
                var outbound = new WhatsAppOutboundMessage(null, null, user.Id, condominium.Id,
                    employee.NormalizedPhoneNumber!, WhatsAppNotificationType.EmployeePayslipAvailable,
                    WhatsAppSendMode.Template, $"employee-document:{document.Id}:whatsapp:1",
                    "Holerite disponivel.", "holerite_disponivel", "pt_BR", DateTime.UtcNow,
                    status: WhatsAppOutboundStatus.Failed, employeeDocumentId: document.Id);
                db.Add(outbound);
                await db.SaveChangesAsync();
                db.Add(new EmployeeDocumentDelivery(document.Id, employee.Id, condominium.Id, batch.Id,
                    EmployeeDocumentDeliveryChannel.WhatsApp, outbound.Id, user.Id, DateTime.UtcNow));
                batch.StartDistributing();
                await db.SaveChangesAsync();
            }
            return (batch.Id, document.Id, user.Id);
        });
    }

    private sealed class FirstBatchLockPause : DbCommandInterceptor
    {
        private int _seen;
        private readonly TaskCompletionSource _acquired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Acquired => _acquired.Task;
        public void Release() => _release.TrySetResult();

        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)
                && Interlocked.Increment(ref _seen) == 1)
            {
                _acquired.TrySetResult();
                await _release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}

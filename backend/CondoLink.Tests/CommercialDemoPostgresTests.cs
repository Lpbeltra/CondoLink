using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using CondoLink.Api.Features.Overwatch.CommercialDemo;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CondoLink.Tests;

public sealed class CommercialDemoPostgresTests
{
    [Fact]
    public async Task Create_use_revert_and_recreate_on_disposable_postgres()
    {
        if (!PostgresPendingActionDatabase.IsConfigured) return;
        var storageRoot = Path.Combine(Path.GetTempPath(), $"comvy-commercial-demo-pg-{Guid.NewGuid():N}");
        try
        {
            await using var database = await PostgresPendingActionDatabase.CreateAsync();
            await using var host = await CoreEndpointTestHost.StartAsync(
                app => app.MapCommercialDemoEndpoints(), builder =>
                {
                    builder.Configuration["FileStorage:RootPath"] = storageRoot;
                    builder.Services.AddSingleton<LocalFileStorage>();
                    builder.Services.AddScoped<CommercialDemoCreator>();
                    builder.Services.AddScoped<CommercialDemoReverter>();
                }, database.ConnectionString);
            var admin = host.ClientFor(Guid.NewGuid());
            admin.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
            var credentials = new
            {
                confirmation = "CREATE commercial-demo-v1",
                managerPassword = NewPassword(), residentPassword = NewPassword(), employeePassword = NewPassword()
            };

            Guid canaryCondo = default, canaryUser = default, canaryProvider = default;
            Guid canaryMembership = default, canaryUnit = default, canaryUnitMembership = default, canaryProviderLink = default;
            await host.WithDbAsync(async db =>
            {
                Assert.Contains("20260923174907_AddCommercialDemoDatasetLedger",
                    await db.Database.GetAppliedMigrationsAsync());
                Assert.Equal("uuid", await db.Database.SqlQueryRaw<string>(
                    "SELECT data_type AS \"Value\" FROM information_schema.columns " +
                    "WHERE table_name = 'commercial_demo_datasets' AND column_name = 'created_by_user_id'").SingleAsync());
                Assert.Equal(0, await db.Database.SqlQueryRaw<int>(
                    "SELECT count(*) AS \"Value\" FROM information_schema.table_constraints " +
                    "WHERE table_name = 'commercial_demo_datasets' AND constraint_type = 'FOREIGN KEY'").SingleAsync());
                Assert.Equal(1, await db.Database.SqlQueryRaw<int>(
                    "SELECT count(*) AS \"Value\" FROM information_schema.table_constraints " +
                    "WHERE table_name = 'commercial_demo_datasets' AND constraint_type = 'PRIMARY KEY'").SingleAsync());
                Assert.False(await db.CommercialDemoDatasets.AnyAsync());
                Assert.False(await db.Condominiums.AnyAsync(x => x.Name == "Residencial Aurora"));
                var condo = new Condominium("Condomínio Controle Revert", "controle@example.invalid", null);
                var user = CoreTestSeed.User("Pessoa Controle", "controle.pessoa@example.invalid");
                var provider = new CondoLink.Domain.Entities.ServiceProvider("Prestador Controle", null, "Elétrica", null,
                    "+55 11 00000-0000", "controle.prestador@example.invalid", null, null, null, DateTime.UtcNow);
                var membership = new CondominiumMembership(user.Id, condo.Id);
                var unit = new Unit(condo.Id, "101", null, "1", null);
                var unitMembership = new UnitMembership(user.Id, unit.Id,
                    CondoLink.Domain.Enums.UnitRelationshipType.Owner, true, true);
                var providerLink = new ServiceProviderCondominiumLink(provider.Id, condo.Id);
                db.AddRange(condo, user, provider, membership, unit, unitMembership, providerLink);
                await db.SaveChangesAsync();
                canaryCondo = condo.Id; canaryUser = user.Id; canaryProvider = provider.Id;
                canaryMembership = membership.Id; canaryUnit = unit.Id;
                canaryUnitMembership = unitMembership.Id; canaryProviderLink = providerLink.Id;
            });
            var absent = await admin.GetFromJsonAsync<Preview>("/overwatch/commercial-demo/revert-preview");
            Assert.NotNull(absent);
            Assert.False(absent.Exists);

            Assert.Equal(HttpStatusCode.Created,
                (await admin.PostAsJsonAsync("/overwatch/commercial-demo/create", credentials)).StatusCode);
            var before = await host.WithDbAsync(async db =>
            {
                var condoId = await db.Condominiums.Where(x => x.Name == "Residencial Aurora")
                    .Select(x => x.Id).SingleAsync();
                var managerId = await db.Users.Where(x => x.Email == "gestao@aurora.invalid")
                    .Select(x => x.Id).SingleAsync();
                var requestId = await db.Requests.Where(x => x.Title == "Portão da garagem falhando")
                    .Select(x => x.Id).SingleAsync();
                Assert.Equal(2, await db.CondominiumBlocks.CountAsync(x => x.CondominiumId == condoId));
                Assert.Equal(24, await db.Units.CountAsync(x => x.CondominiumId == condoId));
                Assert.Equal(10, await db.Requests.CountAsync(x => x.CondominiumId == condoId));
                Assert.Equal(4, await db.AgendaReminders.CountAsync(x => x.CondominiumId == condoId));
                Assert.Equal(2, await db.CondominiumDocuments.CountAsync(x => x.CondominiumId == condoId));
                Assert.Equal(2, await db.ManagementCompanyRequests.CountAsync(x => x.CondominiumId == condoId));
                Assert.Empty(await db.WhatsAppOutboundMessages.ToArrayAsync());
                Assert.Empty(await db.TelegramInboundUpdates.ToArrayAsync());
                return (condoId, managerId, requestId);
            });
            Assert.Equal(HttpStatusCode.OK,
                (await admin.PostAsJsonAsync("/overwatch/commercial-demo/create", credentials)).StatusCode);
            Assert.Equal(1, await host.WithDbAsync(db => db.Condominiums.CountAsync(x => x.Name == "Residencial Aurora")));

            await host.WithDbAsync(async db =>
            {
                db.RefreshSessions.Add(new RefreshSession(before.managerId, Guid.NewGuid().ToString("N"),
                    (await db.Users.SingleAsync(x => x.Id == before.managerId)).SecurityStamp!,
                    DateTime.UtcNow, DateTime.UtcNow.AddDays(1)));
                db.RequestMessages.Add(new RequestMessage(before.requestId, before.managerId, "Atualização de demonstração."));
                db.RequestInternalNotes.Add(new RequestInternalNote(before.requestId, before.managerId, "Nota da equipe demo."));
                await db.SaveChangesAsync();
            });
            _ = await host.WithServicesAsync(s => s.GetRequiredService<CommercialDemoReverter>().InspectAsync(default));
            var preview = await admin.GetFromJsonAsync<Preview>("/overwatch/commercial-demo/revert-preview");
            Assert.NotNull(preview);
            Assert.True(preview.CanRevert);
            Assert.Empty(preview.Conflicts);
            Assert.Equal(0, preview.ExternalRecordsAffected);
            Assert.Equal(1, preview.Counts["RefreshSession"]);
            Assert.Equal(4, preview.Counts["RequestInternalNote"]);
            Assert.Equal(21, preview.Counts["RequestMessage"]);
            Assert.Equal(HttpStatusCode.OK,
                (await admin.PostAsJsonAsync("/overwatch/commercial-demo/revert",
                    new { confirmation = "REVERT commercial-demo-v1" })).StatusCode);
            await host.WithDbAsync(async db =>
            {
                Assert.False(await db.CommercialDemoDatasets.AnyAsync());
                Assert.False(await db.Condominiums.AnyAsync(x => x.Id == before.condoId));
                Assert.False(await db.Requests.AnyAsync(x => x.Id == before.requestId));
                Assert.False(await db.Users.AnyAsync(x => x.Id == before.managerId));
                Assert.True(await db.Condominiums.AnyAsync(x => x.Id == canaryCondo));
                Assert.True(await db.Users.AnyAsync(x => x.Id == canaryUser));
                Assert.True(await db.ServiceProviders.AnyAsync(x => x.Id == canaryProvider));
                Assert.True(await db.CondominiumMemberships.AnyAsync(x => x.Id == canaryMembership));
                Assert.True(await db.Units.AnyAsync(x => x.Id == canaryUnit));
                Assert.True(await db.UnitMemberships.AnyAsync(x => x.Id == canaryUnitMembership));
                Assert.True(await db.ServiceProviderCondominiumLinks.AnyAsync(x => x.Id == canaryProviderLink));
            });
            Assert.Equal(HttpStatusCode.OK,
                (await admin.PostAsJsonAsync("/overwatch/commercial-demo/revert",
                    new { confirmation = "REVERT commercial-demo-v1" })).StatusCode);
            Assert.Equal(HttpStatusCode.Created,
                (await admin.PostAsJsonAsync("/overwatch/commercial-demo/create", credentials)).StatusCode);
            var recreatedId = await host.WithDbAsync(db => db.Condominiums.Where(x => x.Name == "Residencial Aurora")
                .Select(x => x.Id).SingleAsync());
            Assert.Equal(24, await host.WithDbAsync(db => db.Units.CountAsync(x => x.CondominiumId == recreatedId)));
            Assert.True(await host.WithDbAsync(db => db.Condominiums.AnyAsync(x => x.Id == canaryCondo)));
        }
        finally { if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, true); }
    }

    private static string NewPassword() => "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    private sealed record Preview(bool Exists, Dictionary<string, int> Counts,
        string[] Conflicts, int ExternalRecordsAffected, bool CanRevert);
}

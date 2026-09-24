using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using CondoLink.Api.Features.Overwatch.CommercialDemo;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CondoLink.Tests;

public sealed class CommercialDemoEndpointsTests
{
    private static readonly string ManagerPassword = NewPassword();
    private static readonly string ResidentPassword = NewPassword();
    private static readonly string EmployeePassword = NewPassword();

    private static string NewPassword() => "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    [Fact]
    public async Task Create_is_deliberate_idempotent_connected_and_revert_preserves_external_data()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), $"comvy-commercial-demo-{Guid.NewGuid():N}");
        try
        {
            await using var host = await StartAsync(storageRoot);
            var admin = Admin(host);
            Guid externalCondo = default, externalUser = default, externalProvider = default;
            await host.WithDbAsync(async db =>
            {
                var condo = new Condominium("Residencial Externo", "externo@example.invalid", null);
                var user = CoreTestSeed.User("Moradora Externa", "externa@example.invalid");
                var provider = new CondoLink.Domain.Entities.ServiceProvider("Prestador Externo", null, "Elétrica",
                    null, "+55 11 00000-0000", "prestador@example.invalid", null, null, null, DateTime.UtcNow);
                db.AddRange(condo, user, provider);
                await db.SaveChangesAsync();
                externalCondo = condo.Id; externalUser = user.Id; externalProvider = provider.Id;
            });

            var noConfirmation = await admin.PostAsJsonAsync("/overwatch/commercial-demo/create",
                new { confirmation = "", managerPassword = ManagerPassword,
                    residentPassword = ResidentPassword, employeePassword = EmployeePassword });
            Assert.Equal(HttpStatusCode.BadRequest, noConfirmation.StatusCode);
            Assert.False(await host.WithDbAsync(db => db.CommercialDemoDatasets.AnyAsync()));

            var create = await CreateAsync(admin);
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await CreateAsync(admin)).StatusCode);
            await host.WithDbAsync(async db =>
            {
                var demo = await db.Condominiums.SingleAsync(x => x.Name == "Residencial Aurora");
                Assert.Equal(2, await db.CondominiumBlocks.CountAsync(x => x.CondominiumId == demo.Id));
                Assert.Equal(24, await db.Units.CountAsync(x => x.CondominiumId == demo.Id));
                Assert.Equal(10, await db.Requests.CountAsync(x => x.CondominiumId == demo.Id));
                Assert.True(await db.Requests.AnyAsync(x => x.CondominiumId == demo.Id
                    && x.CreatedAt < DateTime.UtcNow.AddDays(-30)));
                Assert.Equal(2, await db.RequestAttachments.CountAsync());
                Assert.Equal(4, await db.AgendaReminders.CountAsync(x => x.CondominiumId == demo.Id));
                Assert.Equal(2, await db.CondominiumDocuments.CountAsync(x => x.CondominiumId == demo.Id));
                Assert.Equal(4, await db.ServiceProviderSpecialties.CountAsync());
                Assert.Equal(2, await db.ManagementCompanyRequests.CountAsync(x => x.CondominiumId == demo.Id));
                Assert.True(await db.UnitMemberships.AnyAsync(x => x.RelationshipType ==
                    CondoLink.Domain.Enums.UnitRelationshipType.AuthorizedOccupant));
                var portao = await db.Requests.SingleAsync(x => x.Title == "Portão da garagem falhando");
                Assert.Equal(2, await db.RequestMessages.CountAsync(x => x.RequestId == portao.Id));
                Assert.NotNull(portao.ServiceProviderId);
                Assert.True(await db.AgendaReminderRequests.AnyAsync(x => x.RequestId == portao.Id));
                Assert.Empty(await db.WhatsAppOutboundMessages.ToArrayAsync());
                Assert.Empty(await db.Notifications.ToArrayAsync());
                Assert.Empty(await db.TelegramInboundUpdates.ToArrayAsync());
                Assert.All(await db.Users.Where(x => x.Email!.EndsWith(".invalid")).ToArrayAsync(),
                    x => Assert.Null(x.PhoneNumber));
                var demoManager = await db.Users.SingleAsync(x => x.Email == "gestao@aurora.invalid");
                db.RefreshSessions.Add(new RefreshSession(demoManager.Id, Guid.NewGuid().ToString("N"),
                    demoManager.SecurityStamp!, DateTime.UtcNow, DateTime.UtcNow.AddDays(2)));
                await db.SaveChangesAsync();
            });

            _ = await host.WithServicesAsync(s => s.GetRequiredService<CommercialDemoReverter>().InspectAsync(default));
            var preview = await admin.GetFromJsonAsync<Preview>("/overwatch/commercial-demo/revert-preview");
            Assert.NotNull(preview);
            Assert.True(preview.Exists);
            Assert.True(preview.CanRevert);
            Assert.Equal(0, preview.ExternalRecordsAffected);
            Assert.Empty(preview.Conflicts);
            Assert.Equal(10, preview.Counts["Request"]);
            Assert.Equal(1, preview.Counts["RefreshSession"]);

            var reverted = await admin.PostAsJsonAsync("/overwatch/commercial-demo/revert",
                new { confirmation = "REVERT commercial-demo-v1" });
            Assert.Equal(HttpStatusCode.OK, reverted.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/overwatch/commercial-demo/revert",
                new { confirmation = "REVERT commercial-demo-v1" })).StatusCode);
            Assert.Empty(Directory.GetFiles(storageRoot, "*", SearchOption.AllDirectories));
            await host.WithDbAsync(async db =>
            {
                Assert.False(await db.CommercialDemoDatasets.AnyAsync());
                Assert.False(await db.Condominiums.AnyAsync(x => x.Name == "Residencial Aurora"));
                Assert.True(await db.Condominiums.AnyAsync(x => x.Id == externalCondo));
                Assert.True(await db.Users.AnyAsync(x => x.Id == externalUser));
                Assert.True(await db.ServiceProviders.AnyAsync(x => x.Id == externalProvider));
            });
        }
        finally { if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, true); }
    }

    [Fact]
    public async Task Revert_refuses_unowned_records_referencing_demo_data()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), $"comvy-commercial-demo-{Guid.NewGuid():N}");
        try
        {
            await using var host = await StartAsync(storageRoot);
            var admin = Admin(host);
            Assert.Equal(HttpStatusCode.Created, (await CreateAsync(admin)).StatusCode);
            await host.WithDbAsync(async db =>
            {
                var condoId = await db.Condominiums.Where(x => x.Name == "Residencial Aurora")
                    .Select(x => x.Id).SingleAsync();
                var externalCondo = new Condominium("Condomínio Visitante", "visitante@example.invalid", null);
                var externalUser = CoreTestSeed.User("Pessoa Visitante", "pessoa@example.invalid");
                db.AddRange(externalCondo, externalUser);
                db.Categories.Add(new Category(condoId, "Categoria externa", null));
                db.AssistantExecutionMetrics.Add(new AssistantExecutionMetric(
                    Guid.NewGuid(), condoId, DateTime.UtcNow, CondoLink.Domain.Enums.CondominiumAssistantChannel.Portal));
                db.CondominiumMemberships.Add(new CondominiumMembership(externalUser.Id, condoId));
                var demoRequestId = await db.Requests.Where(x => x.Title == "Portão da garagem falhando")
                    .Select(x => x.Id).SingleAsync();
                db.RequestMessages.Add(new RequestMessage(demoRequestId, externalUser.Id, "Mensagem externa de controle."));
                var demoProviderId = await db.ServiceProviders.Where(x => x.Name == "Caio Mendes")
                    .Select(x => x.Id).SingleAsync();
                db.ServiceProviderCondominiumLinks.Add(
                    new ServiceProviderCondominiumLink(demoProviderId, externalCondo.Id));
                await db.SaveChangesAsync();
            });
            _ = await host.WithServicesAsync(s => s.GetRequiredService<CommercialDemoReverter>().InspectAsync(default));
            var preview = await admin.GetFromJsonAsync<Preview>("/overwatch/commercial-demo/revert-preview");
            Assert.NotNull(preview);
            Assert.False(preview.CanRevert);
            Assert.NotEmpty(preview.Conflicts);
            Assert.Contains(preview.Conflicts, x => x.Contains("AssistantExecutionMetric"));
            Assert.Contains(preview.Conflicts, x => x.Contains("RequestMessage"));
            var revert = await admin.PostAsJsonAsync("/overwatch/commercial-demo/revert",
                new { confirmation = "REVERT commercial-demo-v1" });
            Assert.Equal(HttpStatusCode.Conflict, revert.StatusCode);
            Assert.True(await host.WithDbAsync(db => db.Condominiums.AnyAsync(x => x.Name == "Residencial Aurora")));
            Assert.True(await host.WithDbAsync(db => db.Users.AnyAsync(x => x.Email == "pessoa@example.invalid")));
            Assert.True(await host.WithDbAsync(db => db.Condominiums.AnyAsync(x => x.Name == "Condomínio Visitante")));
        }
        finally { if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, true); }
    }

    [Fact]
    public async Task Only_platform_admin_can_run_demo_operations()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), $"comvy-commercial-demo-{Guid.NewGuid():N}");
        try
        {
            await using var host = await StartAsync(storageRoot);
            var body = new { confirmation = "CREATE commercial-demo-v1",
                managerPassword = ManagerPassword, residentPassword = ResidentPassword,
                employeePassword = EmployeePassword };
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await host.AnonymousClient().PostAsJsonAsync("/overwatch/commercial-demo/create", body)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await host.ClientFor(Guid.NewGuid()).PostAsJsonAsync("/overwatch/commercial-demo/create", body)).StatusCode);
            Assert.False(await host.WithDbAsync(db => db.CommercialDemoDatasets.AnyAsync()));
        }
        finally { if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, true); }
    }

    [Fact]
    public async Task Revert_tolerates_an_already_removed_owned_leaf()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), $"comvy-commercial-demo-{Guid.NewGuid():N}");
        try
        {
            await using var host = await StartAsync(storageRoot);
            var admin = Admin(host);
            Assert.Equal(HttpStatusCode.Created, (await CreateAsync(admin)).StatusCode);
            await host.WithDbAsync(async db =>
            {
                var specialty = await db.ServiceProviderSpecialties.FirstAsync();
                db.ServiceProviderSpecialties.Remove(specialty);
                await db.SaveChangesAsync();
            });
            var preview = await admin.GetFromJsonAsync<Preview>("/overwatch/commercial-demo/revert-preview");
            Assert.NotNull(preview);
            Assert.Equal(3, preview.Counts["ServiceProviderSpecialty"]);
            Assert.True(preview.CanRevert);
            Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/overwatch/commercial-demo/revert",
                new { confirmation = "REVERT commercial-demo-v1" })).StatusCode);
            Assert.False(await host.WithDbAsync(db => db.CommercialDemoDatasets.AnyAsync()));
        }
        finally { if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, true); }
    }

    private static async Task<CoreEndpointTestHost> StartAsync(string storageRoot) =>
        await CoreEndpointTestHost.StartAsync(app => app.MapCommercialDemoEndpoints(), builder =>
        {
            builder.Configuration["FileStorage:RootPath"] = storageRoot;
            builder.Services.AddSingleton<LocalFileStorage>();
            builder.Services.AddScoped<CommercialDemoCreator>();
            builder.Services.AddScoped<CommercialDemoReverter>();
        });

    private static HttpClient Admin(CoreEndpointTestHost host)
    {
        var client = host.ClientFor(Guid.NewGuid());
        client.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        return client;
    }

    private static Task<HttpResponseMessage> CreateAsync(HttpClient client) =>
        client.PostAsJsonAsync("/overwatch/commercial-demo/create",
            new { confirmation = "CREATE commercial-demo-v1", managerPassword = ManagerPassword,
                residentPassword = ResidentPassword, employeePassword = EmployeePassword });

    private sealed record Preview(bool Exists, Dictionary<string, int> Counts,
        string[] Conflicts, int ExternalRecordsAffected, bool CanRevert);
}

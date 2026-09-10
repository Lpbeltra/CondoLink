using System.Net;
using System.Text.Json;
using CondoLink.Api.Features.WebPush;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Lib.Net.Http.WebPush;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Tests;

public sealed class WebPushDispatcherTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;
    private ApplicationUser _resident = null!;
    private Notification _notification = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        var condominium = new Condominium("Push", null, null);
        _resident = CoreTestSeed.User("Resident", "resident.push@example.com");
        var category = new Category(condominium.Id, "Geral", null);
        var request = new DomainRequest(condominium.Id, _resident.Id, null,
            category.Id, "Assunto privado", "Descrição privada");
        _notification = new Notification(_resident.Id, condominium.Id,
            NotificationType.RequestMessageReceived, "Privado", "Conteúdo privado", request.Id);
        _db.AddRange(condominium, _resident, category, request, _notification,
            new WebPushSubscription(_resident.Id, "https://push.test/a", "p", "a", null, DateTime.UtcNow),
            new WebPushSubscription(_resident.Id, "https://push.test/b", "p", "a", null, DateTime.UtcNow));
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _connection.DisposeAsync(); }

    [Fact]
    public async Task Sends_safe_payload_to_every_active_device()
    {
        var client = new FakeClient();
        await Dispatcher(client).DispatchAsync(_notification.Id, default);
        Assert.Equal(2, client.Payloads.Count);
        Assert.All(client.Payloads, payload =>
        {
            Assert.Contains($"/requests/{_notification.RequestId}", payload);
            Assert.DoesNotContain("Conteúdo privado", payload);
            Assert.DoesNotContain("Assunto privado", payload);
            using var document = JsonDocument.Parse(payload);
            Assert.Equal("Nova resposta", document.RootElement.GetProperty("title").GetString());
            Assert.Equal("Há uma nova resposta no seu atendimento.",
                document.RootElement.GetProperty("body").GetString());
        });
    }

    [Theory]
    [InlineData(NotificationType.RequestCreated, false, "Novo atendimento", "Um novo atendimento foi aberto.")]
    [InlineData(NotificationType.ResidentRequestUpdated, false, "Nova mensagem", "Há uma nova mensagem em um atendimento.")]
    [InlineData(NotificationType.RequestMessageReceived, false, "Nova mensagem", "Há uma nova mensagem em um atendimento.")]
    [InlineData(NotificationType.RequestMessageReceived, true, "Nova resposta", "Há uma nova resposta no seu atendimento.")]
    [InlineData(NotificationType.RequestStatusChanged, true, "Atendimento atualizado", "O status do seu atendimento foi atualizado.")]
    public void Uses_contextual_private_content(NotificationType type, bool residentTarget,
        string title, string body)
    {
        var content = WebPushDispatcher.SafeContent(type, residentTarget);
        Assert.Equal(title, content.Title);
        Assert.Equal(body, content.Body);
        Assert.NotEqual("Comvy", content.Title);
        Assert.DoesNotContain("Assunto privado", content.Body);
        Assert.DoesNotContain("Conteúdo privado", content.Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task Permanent_failure_deactivates_subscription(HttpStatusCode status)
    {
        var client = new FakeClient(status);
        await Dispatcher(client).DispatchAsync(_notification.Id, default);
        Assert.All(await _db.WebPushSubscriptions.ToArrayAsync(), row => Assert.False(row.IsActive));
    }

    [Fact]
    public async Task Transient_failure_does_not_deactivate_subscription()
    {
        var client = new FakeClient(HttpStatusCode.ServiceUnavailable);
        await Dispatcher(client).DispatchAsync(_notification.Id, default);
        Assert.All(await _db.WebPushSubscriptions.ToArrayAsync(), row => Assert.True(row.IsActive));
    }

    private WebPushDispatcher Dispatcher(IWebPushClient client) => new(_db, client,
        Options.Create(new WebPushOptions { Enabled = true, VapidPublicKey = "public",
            VapidPrivateKey = "private", Subject = "mailto:ops@comvy.test" }),
        TimeProvider.System, NullLogger<WebPushDispatcher>.Instance);

    private sealed class FakeClient(HttpStatusCode? status = null) : IWebPushClient
    {
        public List<string> Payloads { get; } = [];
        public Task SendAsync(string endpoint, string p256dh, string auth, string payload,
            CancellationToken cancellationToken)
        {
            Payloads.Add(payload);
            return status.HasValue
                ? Task.FromException(new PushServiceClientException("failed", status.Value))
                : Task.CompletedTask;
        }
    }
}

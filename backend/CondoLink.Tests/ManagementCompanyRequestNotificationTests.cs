using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.ManagementCompanyRequests;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class ManagementCompanyRequestNotificationTests : IAsyncLifetime
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "comvy-mcr-notify", Guid.NewGuid().ToString("N"));
    private readonly RecordingEmailSender email = new();
    private CoreEndpointTestHost host = null!;

    private Guid condoId, unitId;
    private Guid manager, submanager, platformAdmin;
    private Guid accessA, accessJoao, accessMaria;
    private Guid fineCategoryId, reassignCategoryId;

    public async Task InitializeAsync()
    {
        host = await CoreEndpointTestHost.StartAsync(
            app => app.MapManagementCompanyRequests(),
            builder =>
            {
                builder.Configuration["FileStorage:RootPath"] = root;
                builder.Services
                    .AddSingleton<LocalFileStorage>()
                    .AddScoped<ManagementCompanyRequestAccessService>()
                    .AddScoped<ManagementCompanyRequestService>()
                    .AddScoped<ManagementCompanyRequestNotificationService>()
                    .AddSingleton<CondoLink.Api.Features.Auth.IEmailSender>(email)
                    .Configure<FirstAccessOptions>(x => x.FrontendBaseUrl = "https://app.comvy.test");
            });

        await host.WithDbAsync(async db =>
        {
            var condo = new Condominium("Monticello", null, null);
            var unit = new Unit(condo.Id, "101", null, null, null);
            var company = new ManagementCompany("Empresa X", null, null, null, null);
            var otherCompany = new ManagementCompany("Empresa Y", null, null, null, null);

            var m = CoreTestSeed.User("Gestora", "manager@test.local");
            var sm = CoreTestSeed.User("Subgestor", "submanager@test.local");
            var pa = CoreTestSeed.User("Admin Plataforma", "platform-admin@test.local");
            var uA = CoreTestSeed.User("Acesso A", "access-a@test.local");
            var uB = CoreTestSeed.User("Acesso B", "access-b@test.local");
            var uInactive = CoreTestSeed.User("Acesso Inativo", "access-inactive@test.local");
            var uOther = CoreTestSeed.User("Acesso Y", "access-y@test.local");
            var joao = CoreTestSeed.User("Joao", "joao@test.local");
            var maria = CoreTestSeed.User("Maria", "maria@test.local");

            foreach (var u in new[] { m, sm, uA, uB, uInactive, uOther, joao, maria, pa })
                u.SetEmailDeliveryEnabled(true);

            var fineCategory = new ManagementCompanyRequestCategory(company.Id, "Multas", null, ManagementCompanyRequestFormType.UnitFine);
            var otherFormCategory = new ManagementCompanyRequestCategory(company.Id, "Dúvidas", null, ManagementCompanyRequestFormType.Generic);
            var reassignCategory = new ManagementCompanyRequestCategory(company.Id, "Reatribuição", null, ManagementCompanyRequestFormType.Generic);
            var otherCompanyFineCategory = new ManagementCompanyRequestCategory(otherCompany.Id, "Multas Y", null, ManagementCompanyRequestFormType.UnitFine);

            var employeeA = new ManagementCompanyEmployee(company.Id, uA.Id, "Atendimento");
            var employeeB = new ManagementCompanyEmployee(company.Id, uB.Id, "Atendimento");
            var employeeInactive = new ManagementCompanyEmployee(company.Id, uInactive.Id, "Antigo");
            employeeInactive.Deactivate();
            var employeeOther = new ManagementCompanyEmployee(otherCompany.Id, uOther.Id, "Atendimento");
            var employeeJoao = new ManagementCompanyEmployee(company.Id, joao.Id, "Atendimento");
            var employeeMaria = new ManagementCompanyEmployee(company.Id, maria.Id, "Atendimento");

            db.AddRange(condo, unit, company, otherCompany, m, sm, pa, uA, uB, uInactive, uOther, joao, maria,
                fineCategory, otherFormCategory, reassignCategory, otherCompanyFineCategory,
                employeeA, employeeB, employeeInactive, employeeOther, employeeJoao, employeeMaria,
                new CondominiumManagementCompanyLink(condo.Id, company.Id),
                new ManagementCompanyRequestCategoryResponsible(fineCategory.Id, employeeA.Id),
                new ManagementCompanyRequestCategoryResponsible(fineCategory.Id, employeeInactive.Id),
                new ManagementCompanyRequestCategoryResponsible(otherFormCategory.Id, employeeB.Id),
                new ManagementCompanyRequestCategoryResponsible(otherCompanyFineCategory.Id, employeeOther.Id),
                new ManagementCompanyRequestCategoryResponsible(reassignCategory.Id, employeeJoao.Id));

            CoreTestSeed.AddMember(db, m.Id, condo.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(db, sm.Id, condo.Id, CondominiumRole.SubManager);
            await db.SaveChangesAsync();

            condoId = condo.Id; unitId = unit.Id;
            manager = m.Id; submanager = sm.Id; platformAdmin = pa.Id;
            accessA = uA.Id; accessJoao = joao.Id; accessMaria = maria.Id;
            fineCategoryId = fineCategory.Id; reassignCategoryId = reassignCategory.Id;
        });
    }

    public async Task DisposeAsync()
    {
        await host.DisposeAsync();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [Fact]
    public async Task Created_notifies_only_active_recipients_responsible_for_the_category()
    {
        var requestId = await CreateFineAsync(manager, fineCategoryId);

        await host.WithDbAsync(async db =>
        {
            var notifications = await db.Notifications
                .Where(n => n.ManagementCompanyRequestId == requestId).ToListAsync();
            Assert.Single(notifications);
            Assert.Equal(accessA, notifications[0].RecipientUserId);
            Assert.Equal(NotificationType.ManagementCompanyRequestCreated, notifications[0].Type);
        });
        Assert.Single(email.Messages);
        Assert.Equal("access-a@test.local", email.Messages[0].Recipient);
        Assert.Contains("ADM-", email.Messages[0].Subject);
    }

    [Fact]
    public async Task Information_requested_notifies_manager_and_submanager_but_not_platform_admin()
    {
        var requestId = await CreateFineAsync(manager, fineCategoryId);
        await host.ClientFor(accessA).PostAsync($"/management-company-requests/{requestId}/start-processing", null);
        var status = await host.ClientFor(accessA)
            .PostAsJsonAsync($"/management-company-requests/{requestId}/status", new { status = "WaitingManager" });
        Assert.Equal(HttpStatusCode.NoContent, status.StatusCode);

        await host.WithDbAsync(async db =>
        {
            var recipients = await db.Notifications
                .Where(n => n.ManagementCompanyRequestId == requestId
                    && n.Type == NotificationType.ManagementCompanyRequestInfoRequested)
                .Select(n => n.RecipientUserId).ToListAsync();
            Assert.Equal(2, recipients.Count);
            Assert.Contains(manager, recipients);
            Assert.Contains(submanager, recipients);
            Assert.DoesNotContain(platformAdmin, recipients);
        });
        Assert.Equal(2, email.Messages.Count(m => m.Subject.Contains("informação")));
    }

    [Fact]
    public async Task Manager_reply_notifies_the_currently_responsible_access_not_the_previous_one()
    {
        var requestId = await CreateQuestionAsync(manager, reassignCategoryId);
        await host.ClientFor(accessJoao).PostAsync($"/management-company-requests/{requestId}/start-processing", null);
        await host.ClientFor(accessJoao)
            .PostAsJsonAsync($"/management-company-requests/{requestId}/status", new { status = "WaitingManager" });

        // João perde a categoria; Maria passa a ser responsável.
        await host.WithDbAsync(async db =>
        {
            var joaoAccessId = await db.ManagementCompanyEmployees.Where(x => x.UserId == accessJoao).Select(x => x.Id).SingleAsync();
            var mariaAccessId = await db.ManagementCompanyEmployees.Where(x => x.UserId == accessMaria).Select(x => x.Id).SingleAsync();
            var current = await db.ManagementCompanyRequestCategoryResponsibles
                .Where(x => x.ManagementCompanyRequestCategoryId == reassignCategoryId && x.ManagementCompanyEmployeeId == joaoAccessId)
                .ToListAsync();
            db.RemoveRange(current);
            db.Add(new ManagementCompanyRequestCategoryResponsible(reassignCategoryId, mariaAccessId));
            await db.SaveChangesAsync();
        });

        email.Messages.Clear();
        var reply = await host.ClientFor(manager)
            .PostAsJsonAsync($"/management-company-requests/{requestId}/messages", new { content = "Segue a resposta" });
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);

        await host.WithDbAsync(async db =>
        {
            var recipients = await db.Notifications
                .Where(n => n.ManagementCompanyRequestId == requestId
                    && n.Type == NotificationType.ManagementCompanyRequestManagerReplied)
                .Select(n => n.RecipientUserId).ToListAsync();
            Assert.Equal([accessMaria], recipients);
        });
        Assert.Single(email.Messages);
        Assert.Equal("maria@test.local", email.Messages[0].Recipient);
    }

    [Fact]
    public async Task Completed_notifies_manager_and_submanager_with_contextual_label()
    {
        var requestId = await CreateFineAsync(manager, fineCategoryId);
        await host.ClientFor(accessA).PostAsync($"/management-company-requests/{requestId}/start-processing", null);
        email.Messages.Clear();

        var completed = await host.ClientFor(accessA)
            .PostAsJsonAsync($"/management-company-requests/{requestId}/status", new { status = "Completed" });
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);

        await host.WithDbAsync(async db =>
        {
            var notifications = await db.Notifications
                .Where(n => n.ManagementCompanyRequestId == requestId
                    && n.Type == NotificationType.ManagementCompanyRequestCompleted)
                .ToListAsync();
            Assert.Equal(2, notifications.Count);
            Assert.All(notifications, n => Assert.Equal("Multa processada", n.Title));
            Assert.Contains(notifications, n => n.RecipientUserId == manager);
            Assert.Contains(notifications, n => n.RecipientUserId == submanager);
        });
        Assert.Equal(2, email.Messages.Count);
        Assert.All(email.Messages, m => Assert.Contains("Multa processada", m.Subject));
    }

    [Fact]
    public async Task Cancelled_notifies_only_historical_company_recipients_after_administrator_swap()
    {
        Guid swapCondoId = default, swapCompanyYId = default, requestId = default, xAccessUserId = default, linkId = default;
        await host.WithDbAsync(async db =>
        {
            var condo = new Condominium("Swap", null, null);
            var x = new ManagementCompany("X", null, null, null, null);
            var y = new ManagementCompany("Y", null, null, null, null);
            var xUser = CoreTestSeed.User("AcessoX", "x-notify@test.local");
            var yUser = CoreTestSeed.User("AcessoY", "y-notify@test.local");
            xUser.SetEmailDeliveryEnabled(true); yUser.SetEmailDeliveryEnabled(true);
            var catX = new ManagementCompanyRequestCategory(x.Id, "Dúvidas", null, ManagementCompanyRequestFormType.Generic);
            var catY = new ManagementCompanyRequestCategory(y.Id, "Dúvidas", null, ManagementCompanyRequestFormType.Generic);
            var accessX = new ManagementCompanyEmployee(x.Id, xUser.Id, "Atendimento");
            var accessY = new ManagementCompanyEmployee(y.Id, yUser.Id, "Atendimento");
            var link = new CondominiumManagementCompanyLink(condo.Id, x.Id);
            var request = new ManagementCompanyRequest(condo.Id, x.Id, catX.Id, manager, ManagementCompanyRequestType.GeneralQuestion);
            db.AddRange(condo, x, y, xUser, yUser, catX, catY, accessX, accessY, link, request,
                new ManagementCompanyGeneralQuestionRequest(request.Id, "Contrato"),
                new ManagementCompanyRequestCategoryResponsible(catX.Id, accessX.Id),
                new ManagementCompanyRequestCategoryResponsible(catY.Id, accessY.Id));
            CoreTestSeed.AddMember(db, manager, condo.Id, CondominiumRole.Manager);
            await db.SaveChangesAsync();
            swapCondoId = condo.Id; swapCompanyYId = y.Id; requestId = request.Id; xAccessUserId = xUser.Id; linkId = link.Id;
        });
        await host.WithDbAsync(async db =>
        {
            (await db.CondominiumManagementCompanyLinks.SingleAsync(l => l.Id == linkId)).Unlink(DateTime.UtcNow);
            db.CondominiumManagementCompanyLinks.Add(new CondominiumManagementCompanyLink(swapCondoId, swapCompanyYId));
            await db.SaveChangesAsync();
        });
        email.Messages.Clear();

        var cancel = await host.ClientFor(manager)
            .PostAsJsonAsync($"/management-company-requests/{requestId}/cancel", new { reason = "Não é mais necessário" });
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        await host.WithDbAsync(async db =>
        {
            var recipients = await db.Notifications
                .Where(n => n.ManagementCompanyRequestId == requestId
                    && n.Type == NotificationType.ManagementCompanyRequestCancelled)
                .Select(n => n.RecipientUserId).ToListAsync();
            Assert.Equal([xAccessUserId], recipients);
        });
        Assert.Single(email.Messages);
        Assert.Equal("x-notify@test.local", email.Messages[0].Recipient);
    }

    [Fact]
    public async Task Notification_dispatch_is_idempotent_on_retry()
    {
        var requestId = await CreateFineAsync(manager, fineCategoryId);
        Assert.Single(email.Messages);

        var request = await host.WithDbAsync(db => db.ManagementCompanyRequests.AsNoTracking().SingleAsync(x => x.Id == requestId));
        await host.WithServicesAsync(async services =>
        {
            var notifications = services.GetRequiredService<ManagementCompanyRequestNotificationService>();
            await notifications.NotifyCreatedAsync(request, CancellationToken.None);
            await notifications.NotifyCreatedAsync(request, CancellationToken.None);
        });

        await host.WithDbAsync(async db =>
        {
            var count = await db.Notifications.CountAsync(n => n.ManagementCompanyRequestId == requestId);
            Assert.Equal(1, count);
        });
        Assert.Single(email.Messages);
    }

    [Fact]
    public async Task Email_failure_does_not_block_the_request_creation_or_the_internal_notification()
    {
        email.FailFor.Add("access-a@test.local");

        var response = await CreateFineRawAsync(manager, fineCategoryId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var requestId = await ExtractIdAsync(response);

        await host.WithDbAsync(async db =>
        {
            var notification = await db.Notifications.SingleAsync(n => n.ManagementCompanyRequestId == requestId);
            Assert.Equal(accessA, notification.RecipientUserId);
        });
        Assert.Empty(email.Messages);
    }

    [Fact]
    public async Task Notification_does_not_grant_authorization_after_the_recipient_loses_the_category()
    {
        var requestId = await CreateFineAsync(manager, fineCategoryId);
        await host.WithDbAsync(async db =>
        {
            var employeeId = await db.ManagementCompanyEmployees.Where(x => x.UserId == accessA).Select(x => x.Id).SingleAsync();
            db.RemoveRange(await db.ManagementCompanyRequestCategoryResponsibles
                .Where(x => x.ManagementCompanyRequestCategoryId == fineCategoryId && x.ManagementCompanyEmployeeId == employeeId)
                .ToListAsync());
            await db.SaveChangesAsync();
        });

        await host.WithDbAsync(async db =>
            Assert.True(await db.Notifications.AnyAsync(n => n.ManagementCompanyRequestId == requestId && n.RecipientUserId == accessA)));

        var denied = await host.ClientFor(accessA).GetAsync($"/management-company-requests/{requestId}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private async Task<Guid> CreateFineAsync(Guid userId, Guid categoryId)
    {
        var response = await CreateFineRawAsync(userId, categoryId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ExtractIdAsync(response);
    }

    private Task<HttpResponseMessage> CreateFineRawAsync(Guid userId, Guid categoryId) =>
        host.ClientFor(userId).PostAsJsonAsync("/management-company-requests/fines", new
        {
            condominiumId = condoId,
            categoryId,
            unitId,
            nature = "Barulho",
            description = "Perturbação do sossego",
            occurrenceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            value = (decimal?)null,
            valueNotDefined = true,
        });

    private async Task<Guid> CreateQuestionAsync(Guid userId, Guid categoryId)
    {
        var response = await host.ClientFor(userId).PostAsJsonAsync("/management-company-requests/questions", new
        {
            condominiumId = condoId,
            categoryId,
            theme = "Contrato",
            message = "Poderiam revisar o contrato?",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ExtractIdAsync(response);
    }

    private static async Task<Guid> ExtractIdAsync(HttpResponseMessage response)
    {
        var location = response.Headers.Location?.ToString();
        if (!string.IsNullOrEmpty(location)) return Guid.Parse(location.Split('/')[^1]);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private sealed class RecordingEmailSender : CondoLink.Api.Features.Auth.IEmailSender
    {
        public List<(string Recipient, string Subject, string Html)> Messages { get; } = [];
        public HashSet<string> FailFor { get; } = [];
        public Task SendAsync(string recipient, string subject, string html, CancellationToken cancellationToken)
        {
            if (FailFor.Contains(recipient)) throw new InvalidOperationException("Simulated SMTP failure.");
            Messages.Add((recipient, subject, html));
            return Task.CompletedTask;
        }
    }
}

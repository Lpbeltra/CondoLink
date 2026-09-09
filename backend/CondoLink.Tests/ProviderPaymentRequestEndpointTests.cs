using System.Net;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.ManagementCompanyRequests;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Api.Features.Requests;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProviderEntity = CondoLink.Domain.Entities.ServiceProvider;

namespace CondoLink.Tests;

public sealed class ProviderPaymentRequestEndpointTests : IAsyncLifetime
{
    private CoreEndpointTestHost host = null!; private Guid manager, requestId, providerId, companyId;
    public async Task InitializeAsync()
    {
        host = await CoreEndpointTestHost.StartAsync(app => { app.MapRequestServiceProviderEndpoints(); app.MapGetRequestById(); }, builder =>
        {
            builder.Services.AddSignalR(); builder.Services.AddSingleton<LocalFileStorage>();
            builder.Services.AddScoped<ManagementCompanyRequestAccessService>(); builder.Services.AddScoped<ManagementCompanyRequestService>();
            builder.Services.AddScoped<ManagementCompanyRequestNotificationService>(); builder.Services.AddScoped<ManagementCompanyRequestRealtimeService>();
            builder.Services.AddSingleton<IEmailSender>(new NoOpEmailSender());
        });
        await host.WithDbAsync(async db =>
        {
            var condo = new Condominium("Condomínio A", null, null); var company = new ManagementCompany("Administradora", null, null, null, null);
            var user = CoreTestSeed.User("Lisandro", "manager@provider.test"); var employeeUser = CoreTestSeed.User("Atendente", "employee@provider.test");
            var provider = new ProviderEntity("César", null, "Hidráulica", null, "11999999999", null, "cesar@pix.test", ServiceProviderPixKeyType.Email, null, DateTime.UtcNow);
            var category = new ManagementCompanyRequestCategory(company.Id, "Pagamentos", null, ManagementCompanyRequestFormType.SupplierPayment); var attendanceCategory = new Category(condo.Id, "Manutenção", null);
            var employee = new ManagementCompanyEmployee(company.Id, employeeUser.Id, "Financeiro");
            var request = new Request(condo.Id, user.Id, null, attendanceCategory.Id, "Vazamento", "Resolver vazamento"); request.SetServiceProvider(provider.Id, DateTime.UtcNow);
            db.AddRange(condo, company, user, employeeUser, provider, category, attendanceCategory, employee, request, new CondominiumManagementCompanyLink(condo.Id, company.Id), new ServiceProviderCondominiumLink(provider.Id, condo.Id), new ManagementCompanyRequestCategoryResponsible(category.Id, employee.Id));
            CoreTestSeed.AddMember(db, user.Id, condo.Id, CondominiumRole.Manager); await db.SaveChangesAsync();
            manager = user.Id; requestId = request.Id; providerId = provider.Id; companyId = company.Id;
        });
    }
    public Task DisposeAsync() => host.DisposeAsync().AsTask();

    [Fact]
    public async Task Creates_persisted_payment_with_provider_and_pix_snapshots_and_projects_timeline()
    {
        var first = await Create("manual@pix.test", "Email", 200m); var second = await Create("outra@pix.test", "Email", 150m);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode); Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        await host.WithDbAsync(async db =>
        {
            var rows = await db.ManagementCompanyRequests.Where(x => x.RequestId == requestId).OrderBy(x => x.CreatedAt).ToListAsync(); Assert.Equal(2, rows.Count);
            Assert.All(rows, row => { Assert.Equal(ManagementCompanyRequestType.Payment, row.Type); Assert.Equal(companyId, row.ManagementCompanyId); });
            var payments = await db.ManagementCompanyPaymentRequests.Where(x => rows.Select(r => r.Id).Contains(x.RequestId)).ToListAsync();
            Assert.All(payments, payment => { Assert.Equal(providerId, payment.ServiceProviderId); Assert.Equal("César", payment.ThirdPartyIdentification); Assert.Equal(ManagementCompanyPaymentThirdPartyForm.Pix, payment.ThirdPartyForm); Assert.Equal(PixKeyType.Email, payment.ThirdPartyPixKeyType); });
            Assert.Contains(payments, x => x.Value == 200m && x.ThirdPartyPixKey == "manual@pix.test"); Assert.Contains(payments, x => x.Value == 150m && x.ThirdPartyPixKey == "outra@pix.test");
            var source = await db.Requests.SingleAsync(x => x.Id == requestId); source.SetServiceProvider(null, DateTime.UtcNow); await db.SaveChangesAsync();
            Assert.Equal(2, await db.ManagementCompanyRequests.CountAsync(x => x.RequestId == requestId));
        });
        using var detail = JsonDocument.Parse(await host.ClientFor(manager).GetStringAsync($"/requests/{requestId}"));
        var events = detail.RootElement.GetProperty("providerPaymentRequests").EnumerateArray().ToArray(); Assert.Equal(2, events.Length); Assert.All(events, item => Assert.Equal("César", item.GetProperty("providerName").GetString()));
    }

    [Fact]
    public async Task Rejects_missing_link_or_administrator_configuration()
    {
        await host.WithDbAsync(async db => { (await db.Requests.SingleAsync(x => x.Id == requestId)).SetServiceProvider(null, DateTime.UtcNow); await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.BadRequest, (await Create("manual@pix.test", "Email", 1m)).StatusCode);
        await host.WithDbAsync(async db => { var request = await db.Requests.SingleAsync(x => x.Id == requestId); request.SetServiceProvider(providerId, DateTime.UtcNow); db.Remove(await db.CondominiumManagementCompanyLinks.SingleAsync()); await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Conflict, (await Create("manual@pix.test", "Email", 1m)).StatusCode);
    }

    private Task<HttpResponseMessage> Create(string pixKey, string pixKeyType, decimal value)
    {
        var form = new MultipartFormDataContent(); form.Add(new StringContent(JsonSerializer.Serialize(new { nature = "Pagamento de César", value, eventDate = new DateOnly(2026, 9, 8), dueDate = new DateOnly(2026, 9, 20), notes = "Teste", pixKey, pixKeyType }), Encoding.UTF8, "application/json"), "payload");
        return host.ClientFor(manager).PostAsync($"/requests/{requestId}/provider-payment-request", form);
    }
}

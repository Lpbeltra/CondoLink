using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Api.Features.EmployeeDocuments;
using CondoLink.Api.Features.EmployeeManagement;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

/// <summary>Contract tests for the administrator-scoped EmployeeDocuments API.</summary>
public sealed class EmployeeDocumentsEndpointTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private Guid _operatorId;
    private Guid _companyId;
    private Guid _employeeId;
    private Guid _otherEmployeeId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(
            app => app.MapEmployeeDocumentEndpoints(),
            builder =>
            {
                builder.Configuration["FileStorage:RootPath"] = Path.Combine(Path.GetTempPath(), "condolink-employee-document-tests", Guid.NewGuid().ToString("N"));
                builder.Services.AddSingleton<LocalFileStorage>();
                builder.Services.AddScoped<EmployeeManagementAccessService>();
                builder.Services.AddScoped<EmployeeDocumentProcessingService>();
                builder.Services.AddScoped<EmployeeDocumentProcessingWorker>();
                builder.Services.Configure<EmployeeDocumentProcessingOptions>(_ => { });
                builder.Services.AddScoped<EmployeeDocumentDistributionService>();
                builder.Services.AddScoped<CondominiumDocumentProcessor>();
                builder.Services.AddSingleton<IEmbeddingService, LocalEmbeddingService>();
                builder.Services.Configure<CondominiumAssistantOptions>(_ => { });
                builder.Services.Configure<DocumentOcrOptions>(_ => { });
                builder.Services.Configure<WhatsAppOptions>(_ => { });
                builder.Services.AddSingleton<IWhatsAppClient, NoOpWhatsAppClient>();
            });

        await _host.WithDbAsync(async db =>
        {
            var company = new ManagementCompany("Administradora de Holerites", null, null, null, null);
            var user = CoreTestSeed.User("Operador de Holerites", $"employee-docs-{Guid.NewGuid():N}@test.local");
            var companyEmployee = new ManagementCompanyEmployee(company.Id, user.Id, "Departamento pessoal");
            var condoA = new Condominium("Monticello", null, "12345678000190", null, null, null, false, false, null);
            var condoB = new Condominium("Montpellier", null, "98765432000190", null, null, null, false, false, null);
            condoA.SetManagementCompany(company.Id);
            condoB.SetManagementCompany(company.Id);
            var employeeA = new Employee(condoA.Id, "Ana A", null, "11999990001", null, null, null);
            var employeeB = new Employee(condoB.Id, "Bruno B", null, "11999990002", null, null, null);
            db.AddRange(company, user, companyEmployee, condoA, condoB, employeeA, employeeB,
                new CondominiumManagementCompanyLink(condoA.Id, company.Id),
                new CondominiumManagementCompanyLink(condoB.Id, company.Id),
                new ManagementCompanyModule(company.Id, ManagementCompanyModuleType.EmployeeManagement, true, DateTime.UtcNow),
                new ManagementCompanyEmployeeModuleGrant(companyEmployee.Id, ManagementCompanyModuleType.EmployeeManagement, user.Id, DateTime.UtcNow));
            await db.SaveChangesAsync();
            _operatorId = user.Id;
            _companyId = company.Id;
            _employeeId = employeeA.Id;
            _otherEmployeeId = employeeB.Id;
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Global_administrator_can_list_and_upload_one_multi_condominium_batch()
    {
        using var client = _host.ClientFor(_operatorId);
        var list = await client.GetFromJsonAsync<JsonElement>("/administrator/employees/documents/batches");
        Assert.True(list.ValueKind is JsonValueKind.Array);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("8"), "competenceMonth");
        form.Add(new StringContent("2026"), "competenceYear");
        form.Add(new StringContent(_employeeId.ToString()), "employeeIds");
        form.Add(new StringContent(_otherEmployeeId.ToString()), "employeeIds");
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4 test"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "files", "holerites.pdf");

        var response = await client.PostAsync("/administrator/employees/documents/batches", form);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = body.GetProperty("id").GetGuid();

        var batch = await _host.WithDbAsync(db => db.EmployeeDocumentBatches.AsNoTracking().SingleAsync(x => x.Id == batchId));
        Assert.Equal(_companyId, batch.ManagementCompanyId);
        var selected = await _host.WithDbAsync(db => db.EmployeeDocumentBatchEmployees.AsNoTracking().Where(x => x.BatchId == batchId).Select(x => x.EmployeeId).ToArrayAsync());
        Assert.Equal(new[] { _employeeId, _otherEmployeeId }.OrderBy(x => x).ToArray(), selected.OrderBy(x => x).ToArray());
        Assert.Null(batch.CondominiumId);
    }

    [Fact]
    public async Task Legacy_condominium_route_does_not_grant_employee_management_access()
    {
        using var client = _host.ClientFor(_operatorId);
        var response = await client.GetAsync($"/condominiums/{Guid.NewGuid()}/employees/documents/batches");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Module_off_is_fail_closed_even_with_global_grant()
    {
        await _host.WithDbAsync(async db =>
        {
            var module = await db.ManagementCompanyModules.SingleAsync(x => x.ManagementCompanyId == _companyId);
            module.SetEnabled(false, DateTime.UtcNow);
            await db.SaveChangesAsync();
        });
        using var client = _host.ClientFor(_operatorId);
        var response = await client.GetAsync("/administrator/employees/documents/batches");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed class NoOpWhatsAppClient : IWhatsAppClient
    {
        public Task<WhatsAppSendResult> SendTextAsync(string phoneNumber, string text, CancellationToken cancellationToken) => Task.FromResult(new WhatsAppSendResult(true, "test", null));
        public Task<WhatsAppMediaResult> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken) => Task.FromResult(new WhatsAppMediaResult(false, null, null, "disabled"));
        public Task<WhatsAppSendResult> SendTemplateAsync(string phoneNumber, string templateName, string language, IReadOnlyList<string> bodyParameters, IReadOnlyList<string> quickReplyPayloads, CancellationToken cancellationToken, string? bodyParameterName = null) => Task.FromResult(new WhatsAppSendResult(true, "test", null));
        public Task<WhatsAppSendResult> SendTemplateAsync(string phoneNumber, string templateName, string language, IReadOnlyList<string> bodyParameters, IReadOnlyList<string> quickReplyPayloads, CancellationToken cancellationToken, string? bodyParameterName, IReadOnlyList<string> urlButtonParameters, WhatsAppDocumentHeader? documentHeader) => Task.FromResult(new WhatsAppSendResult(true, "test", null));
        public Task<WhatsAppMediaUploadResult> UploadDocumentAsync(byte[] content, string fileName, string mimeType, CancellationToken cancellationToken) => Task.FromResult(new WhatsAppMediaUploadResult(true, "test", null));
    }
}

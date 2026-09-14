using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Api.Features.CondominiumMembers;
using CondoLink.Api.Features.EmployeeDocuments;
using CondoLink.Api.Features.EmployeeManagement;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
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
                builder.Services.AddSingleton<IDocumentOcrService, DisabledOcrService>();
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
            var employeeA = new Employee(condoA.Id, "Ana A", "52998224725", null, "11999990001", null, null, null);
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
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4 test"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "files", "holerites.pdf");

        var response = await client.PostAsync("/administrator/employees/documents/batches", form);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = body.GetProperty("id").GetGuid();

        var batch = await _host.WithDbAsync(db => db.EmployeeDocumentBatches.AsNoTracking().SingleAsync(x => x.Id == batchId));
        Assert.Equal(_companyId, batch.ManagementCompanyId);
        Assert.Null(batch.CondominiumId);
        // No pre-selection anymore: nobody is associated with the batch until the
        // background worker actually processes the uploaded PDFs and identifies them.
        var selected = await _host.WithDbAsync(db => db.EmployeeDocumentBatchEmployees.AsNoTracking().Where(x => x.BatchId == batchId).Select(x => x.EmployeeId).ToArrayAsync());
        Assert.Empty(selected);
    }

    [Fact]
    public async Task Uploaded_batch_is_matched_without_any_preselection_and_populates_batch_scope()
    {
        using var client = _host.ClientFor(_operatorId);
        var pdfBytes = BuildTextPdf("HOLERITE\nFuncionario: Ana A\nCPF: 529.982.247-25\nCNPJ: 12.345.678/0001-90");
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("8"), "competenceMonth");
        form.Add(new StringContent("2026"), "competenceYear");
        var file = new ByteArrayContent(pdfBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "files", "holerite.pdf");

        var response = await client.PostAsync("/administrator/employees/documents/batches", form);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var batchId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Nothing was pre-selected on upload — the worker discovers who this belongs
        // to purely from the PDF content, scoped to the management company's employees.
        await _host.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var processor = sp.GetRequiredService<EmployeeDocumentProcessingService>();
            var batch = await db.EmployeeDocumentBatches.SingleAsync(x => x.Id == batchId);
            await processor.ProcessAsync(batch, default);
        });

        var documents = await _host.WithDbAsync(db => db.EmployeeDocuments.AsNoTracking().Where(x => x.BatchId == batchId).ToArrayAsync());
        var document = Assert.Single(documents);
        Assert.Equal(_employeeId, document.EmployeeId);
        Assert.Equal(EmployeeDocumentIdentificationStatus.Identified, document.IdentificationStatus);
        Assert.Equal("52998224725", document.ExtractedCpfDigits);
        Assert.Equal("12345678000190", document.ExtractedCnpjDigits);

        var scoped = await _host.WithDbAsync(db => db.EmployeeDocumentBatchEmployees.AsNoTracking().Where(x => x.BatchId == batchId).Select(x => x.EmployeeId).ToArrayAsync());
        Assert.Equal([_employeeId], scoped);
    }

    private static byte[] BuildTextPdf(string text)
    {
        PdfSharp.Fonts.GlobalFontSettings.FontResolver ??= new ComvyFontResolver();
        using var document = new PdfSharp.Pdf.PdfDocument();
        var page = document.AddPage();
        using var graphics = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
        var y = 40;
        foreach (var line in text.Split('\n'))
        {
            graphics.DrawString(line, new PdfSharp.Drawing.XFont("ComvySans", 12), PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XPoint(40, y));
            y += 20;
        }
        using var buffer = new MemoryStream();
        document.Save(buffer, false);
        return buffer.ToArray();
    }

    [Fact]
    public async Task Regression_employee_moved_condominiums_an_old_payslip_pdf_does_not_follow_them()
    {
        // Renato used to work at Mendonza. His record was corrected to point at
        // Monticello. An OLD payslip PDF — same person, same CPF, but still
        // carrying Mendonza's letterhead/CNPJ — must NOT be silently associated
        // with him under his new condominium (real bug this guards against).
        const string mendonzaCnpj = "99888777000162";
        var batchId = await _host.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var storage = sp.GetRequiredService<LocalFileStorage>();

            var mendonza = new Condominium("Mendonza", null, mendonzaCnpj, null, null, null, false, false, null);
            mendonza.SetManagementCompany(_companyId);
            var monticello = new Condominium("Monticello Novo", null, "11222333000181", null, null, null, false, false, null);
            monticello.SetManagementCompany(_companyId);
            var renato = new Employee(mendonza.Id, "Renato Paranhos de Araujo", "52998224725", null, null, null, null, null);
            db.AddRange(mendonza, monticello, renato,
                new CondominiumManagementCompanyLink(mendonza.Id, _companyId),
                new CondominiumManagementCompanyLink(monticello.Id, _companyId));
            await db.SaveChangesAsync();

            // The correction: Renato now belongs to Monticello, not Mendonza.
            renato.MoveToCondominium(monticello.Id);
            await db.SaveChangesAsync();

            var batch = new EmployeeDocumentBatch(null, _companyId, EmployeeDocumentType.Payslip, 8, 2026, _operatorId, DateTime.UtcNow);
            var pdfBytes = BuildTextPdf("CONDOMINIO MENDONZA\nCNPJ: 99.888.777/0001-62\nFuncionario: Renato Paranhos de Araujo\nCPF: 529.982.247-25");
            var key = await storage.SaveEmployeeDocumentBatchFileAsync(_companyId, batch.Id, new MemoryStream(pdfBytes), ".pdf", default);
            batch.AttachPendingUploads(JsonSerializer.Serialize(new[] { new PendingEmployeeDocumentUpload(key, "holerite-antigo.pdf") }));
            db.Add(batch);
            await db.SaveChangesAsync();

            var processor = sp.GetRequiredService<EmployeeDocumentProcessingService>();
            await processor.ProcessAsync(batch, default);
            return batch.Id;
        });

        var document = await _host.WithDbAsync(db => db.EmployeeDocuments.AsNoTracking().SingleAsync(x => x.BatchId == batchId));
        Assert.Null(document.EmployeeId); // NOT associated with Renato
        Assert.Null(document.CondominiumId); // Monticello was NOT forced onto this old document
        Assert.NotEqual(EmployeeDocumentIdentificationStatus.Identified, document.IdentificationStatus);
        Assert.NotEqual(EmployeeDocumentIdentificationStatus.Confirmed, document.IdentificationStatus);
        Assert.Equal("52998224725", document.ExtractedCpfDigits);
        Assert.Equal(mendonzaCnpj, document.ExtractedCnpjDigits);
    }

    [Fact]
    public async Task Manual_assign_is_blocked_when_documents_extracted_cpf_contradicts_the_chosen_employee()
    {
        var (batchId, documentId) = await _host.WithDbAsync(async db =>
        {
            var batch = new EmployeeDocumentBatch(null, _companyId, EmployeeDocumentType.Payslip, 8, 2026, _operatorId, DateTime.UtcNow);
            batch.StartProcessing();
            batch.MarkReadyForReview();
            var document = new EmployeeDocument(null, batch.Id, EmployeeDocumentType.Payslip, 8, 2026, "pending", "holerite.pdf", 1, 1, "hash", DateTime.UtcNow);
            document.SetFileKey("stored-key");
            // The PDF explicitly names a CPF that belongs to nobody in this batch —
            // simulating a document whose own content contradicts a would-be manual pick.
            document.ApplyAutomaticIdentification(null, EmployeeDocumentIdentificationConfidence.None,
                EmployeeDocumentIdentificationMethod.None, DateTime.UtcNow, "00000000191", null);
            db.AddRange(batch, document);
            await db.SaveChangesAsync();
            return (batch.Id, document.Id);
        });

        using var client = _host.ClientFor(_operatorId);
        var response = await client.PatchAsJsonAsync($"/administrator/employees/documents/batches/{batchId}/documents/{documentId}",
            new { action = "Assign", employeeId = _employeeId });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var reloaded = await _host.WithDbAsync(db => db.EmployeeDocuments.AsNoTracking().SingleAsync(x => x.Id == documentId));
        Assert.Null(reloaded.EmployeeId); // the hard gate must block the association, not just warn
        var scoped = await _host.WithDbAsync(db => db.EmployeeDocumentBatchEmployees.AsNoTracking().Where(x => x.BatchId == batchId).ToArrayAsync());
        Assert.Empty(scoped);
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

    private sealed class DisabledOcrService : IDocumentOcrService
    {
        public bool Enabled => false;
        public Task<string?> ExtractTextAsync(byte[] imageBytes, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("OCR must not run when disabled.");
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

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Api.Features.CondominiumModules;
using CondoLink.Api.Features.EmployeeDocuments;
using CondoLink.Api.Features.EmployeeManagement;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace CondoLink.Tests;

public sealed class EmployeeDocumentsEndpointTests : IAsyncLifetime
{
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "condolink-employee-documents-tests", Guid.NewGuid().ToString("N"));
    private CoreEndpointTestHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(
            app => app.MapEmployeeDocumentEndpoints(),
            builder =>
            {
                builder.Configuration["FileStorage:RootPath"] = _storageRoot;
                builder.Services.AddSingleton<LocalFileStorage>();
                builder.Services.AddScoped<ICondominiumModuleService, CondominiumModuleService>();
                builder.Services.AddScoped<EmployeeManagementAccessService>();
                builder.Services.AddScoped<EmployeeDocumentProcessingService>();
                builder.Services.AddScoped<EmployeeDocumentProcessingWorker>();
                builder.Services.Configure<EmployeeDocumentProcessingOptions>(_ => { });
                builder.Services.AddScoped<EmployeeDocumentDistributionService>();
                builder.Services.AddScoped<CondominiumDocumentProcessor>();
                builder.Services.AddSingleton<IEmbeddingService, LocalEmbeddingService>();
                builder.Services.AddSingleton<IDocumentOcrService, DisabledOcrService>();
                builder.Services.Configure<CondominiumAssistantOptions>(_ => { });
                builder.Services.Configure<DocumentOcrOptions>(_ => { });
                builder.Services.Configure<WhatsAppOptions>(_ => { });
                builder.Services.AddSingleton<IWhatsAppClient>(_whatsAppClient);
                builder.Services.AddSingleton<IPhoneVerificationMessageProtector, PassthroughVerificationProtector>();
                builder.Services.AddSingleton<IFirstAccessWhatsAppPayloadProtector, UnusedFirstAccessProtector>();
            });
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
        if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, true);
    }

    private readonly CapturingWhatsAppClient _whatsAppClient = new();

    private static async Task EnableModuleAsync(CoreEndpointTestHost host, Guid condominiumId, bool managementCompanyAccessEnabled = false) =>
        await host.WithDbAsync(async db =>
        {
            var row = await db.CondominiumModules.SingleAsync(x => x.CondominiumId == condominiumId && x.Module == CondominiumModuleType.EmployeeManagement);
            row.Set(true, managementCompanyAccessEnabled, DateTime.UtcNow);
            await db.SaveChangesAsync();
        });

    private static byte[] BuildPdf(params string[] pageTexts)
    {
        GlobalFontSettings.FontResolver ??= new CondoLink.Api.Features.CondominiumMembers.ComvyFontResolver();
        var document = new PdfDocument();
        foreach (var text in pageTexts)
        {
            var page = document.AddPage();
            using var graphics = XGraphics.FromPdfPage(page);
            var font = new XFont("ComvySans", 11);
            var y = 40.0;
            foreach (var line in text.Split('\n'))
            {
                graphics.DrawString(line, font, XBrushes.Black, new XPoint(40, y));
                y += 16;
            }
        }
        using var buffer = new MemoryStream();
        document.Save(buffer, false);
        return buffer.ToArray();
    }

    private static async Task<(Guid CondominiumId, Guid BatchId)> UploadAsync(
        HttpClient client, Guid condominiumId, byte[] pdfBytes, string fileName = "folha.pdf",
        int month = 8, int year = 2026)
    {
        var result = await UploadFilesAsync(client, condominiumId, [(fileName, pdfBytes)], month, year);
        result.Response.EnsureSuccessStatusCode();
        return (condominiumId, result.BatchId!.Value);
    }

    private static async Task<(HttpResponseMessage Response, Guid? BatchId)> UploadFilesAsync(
        HttpClient client, Guid condominiumId, (string FileName, byte[] Bytes)[] files,
        int month = 8, int year = 2026, bool expectSuccess = true)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(month.ToString()), "competenceMonth" },
            { new StringContent(year.ToString()), "competenceYear" },
        };
        foreach (var file in files)
        {
            var fileContent = new ByteArrayContent(file.Bytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            content.Add(fileContent, "files", file.FileName);
        }
        var response = await client.PostAsync($"/condominiums/{condominiumId}/employees/documents/batches", content);
        if (!expectSuccess) return (response, null);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (response, body.GetProperty("id").GetGuid());
    }

    private async Task DispatchWhatsAppAsync() =>
        await _host.WithServicesAsync(async services =>
        {
            var worker = new WhatsAppOutboundWorker(
                new SingleScopeFactory(services),
                Microsoft.Extensions.Options.Options.Create(new WhatsAppOptions { Enabled = true, OutboundWorkerEnabled = true }),
                new CondoLink.Api.Features.Observability.OperationalTelemetry(new SingleScopeFactory(services), TimeProvider.System),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppOutboundWorker>.Instance);
            await worker.ProcessBatch(new WhatsAppOptions { Enabled = true, OutboundWorkerEnabled = true }, default);
        });

    private sealed class SingleScopeFactory(IServiceProvider services) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new SingleScope(services);
        private sealed class SingleScope(IServiceProvider services) : IServiceScope
        {
            public IServiceProvider ServiceProvider => services;
            public void Dispose() { }
        }
    }

    private async Task ProcessAsync(Guid batchId) =>
        await _host.WithServicesAsync(async services =>
        {
            var db = services.GetRequiredService<CondoLink.Infrastructure.Persistence.AppDbContext>();
            var batch = await db.EmployeeDocumentBatches.SingleAsync(x => x.Id == batchId);
            await services.GetRequiredService<EmployeeDocumentProcessingService>().ProcessAsync(batch, default);
        });

    [Fact]
    public async Task Upload_splits_a_multi_employee_pdf_and_identifies_each_page()
    {
        var condominium = new Condominium("Condo Holerites A", null, null);
        var manager = CoreTestSeed.User("Síndico Holerites", "sindico-holerites-a@test.local");
        Guid joaoId = Guid.Empty, mariaId = Guid.Empty;
        await _host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            var joao = new Employee(condominium.Id, "João da Silva", "Porteiro", null, null, "MAT-001", null);
            var maria = new Employee(condominium.Id, "Maria Aparecida Souza", "Zeladora", null, null, "MAT-002", null);
            db.AddRange(joao, maria);
            await db.SaveChangesAsync();
            joaoId = joao.Id; mariaId = maria.Id;
        });
        await EnableModuleAsync(_host, condominium.Id);
        var client = _host.ClientFor(manager.Id);

        var pdf = BuildPdf("RECIBO DE PAGAMENTO\nMatricula: MAT-001\nJoao da Silva", "RECIBO DE PAGAMENTO\nMatricula: MAT-002\nMaria Aparecida Souza");
        var (_, batchId) = await UploadAsync(client, condominium.Id, pdf);
        await ProcessAsync(batchId);

        var result = await client.GetFromJsonAsync<JsonElement>($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}");
        var documents = result.GetProperty("documents").EnumerateArray().ToArray();
        Assert.Equal(2, documents.Length);
        Assert.Equal(joaoId.ToString(), documents[0].GetProperty("employeeId").GetString());
        Assert.Equal("Identified", documents[0].GetProperty("identificationStatus").GetString());
        Assert.Equal(mariaId.ToString(), documents[1].GetProperty("employeeId").GetString());
        Assert.Equal("High", documents[1].GetProperty("identificationConfidence").GetString());
    }

    [Fact]
    public async Task Multi_page_payslip_without_a_repeated_signal_stays_one_document()
    {
        var condominium = new Condominium("Condo Holerites B", null, null);
        var manager = CoreTestSeed.User("Síndico B", "sindico-holerites-b@test.local");
        await _host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            db.Add(new Employee(condominium.Id, "João da Silva", null, null, null, "MAT-001", null));
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(_host, condominium.Id);
        var client = _host.ClientFor(manager.Id);

        var pdf = BuildPdf("RECIBO DE PAGAMENTO\nMatricula: MAT-001\nJoao da Silva\nPagina 1 de 2",
            "continuacao do holerite sem novo identificador");
        var (_, batchId) = await UploadAsync(client, condominium.Id, pdf);
        await ProcessAsync(batchId);

        var result = await client.GetFromJsonAsync<JsonElement>($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}");
        var documents = result.GetProperty("documents").EnumerateArray().ToArray();
        Assert.Single(documents);
        Assert.Equal(1, documents[0].GetProperty("pageStart").GetInt32());
        Assert.Equal(2, documents[0].GetProperty("pageEnd").GetInt32());
    }

    [Fact]
    public async Task Review_assign_ignore_and_confirm_gate_batch_confirmation()
    {
        var condominium = new Condominium("Condo Holerites C", null, null);
        var manager = CoreTestSeed.User("Síndico C", "sindico-holerites-c@test.local");
        Guid employeeId = Guid.Empty;
        await _host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            var employee = new Employee(condominium.Id, "Funcionário Sem Matrícula", null, null, null, null, null);
            db.Add(employee);
            await db.SaveChangesAsync();
            employeeId = employee.Id;
        });
        await EnableModuleAsync(_host, condominium.Id);
        var client = _host.ClientFor(manager.Id);

        var firstFile = BuildPdf("Recibo de pagamento sem qualquer identificacao de funcionario.");
        var secondFile = BuildPdf("Outro recibo de pagamento tambem sem identificacao alguma.");
        var uploaded = await UploadFilesAsync(client, condominium.Id, [("folha1.pdf", firstFile), ("folha2.pdf", secondFile)]);
        uploaded.Response.EnsureSuccessStatusCode();
        var batchId = uploaded.BatchId!.Value;
        await ProcessAsync(batchId);

        var beforeConfirm = await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/confirm", null);
        Assert.Equal(HttpStatusCode.Conflict, beforeConfirm.StatusCode);

        var batchDetail = await client.GetFromJsonAsync<JsonElement>($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}");
        var documentIds = batchDetail.GetProperty("documents").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();

        var assign = await client.PatchAsJsonAsync($"/condominiums/{condominium.Id}/employees/documents/{documentIds[0]}",
            new UpdateEmployeeDocumentAssociationRequest("Assign", employeeId));
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        var confirmDoc = await client.PatchAsJsonAsync($"/condominiums/{condominium.Id}/employees/documents/{documentIds[0]}",
            new UpdateEmployeeDocumentAssociationRequest("Confirm", null));
        Assert.Equal(HttpStatusCode.OK, confirmDoc.StatusCode);

        var ignore = await client.PatchAsJsonAsync($"/condominiums/{condominium.Id}/employees/documents/{documentIds[1]}",
            new UpdateEmployeeDocumentAssociationRequest("Ignore", null));
        Assert.Equal(HttpStatusCode.OK, ignore.StatusCode);

        var confirmBatch = await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/confirm", null);
        Assert.Equal(HttpStatusCode.NoContent, confirmBatch.StatusCode);
    }

    [Fact]
    public async Task Batch_and_document_from_another_condominium_are_not_found()
    {
        var condominiumA = new Condominium("Condo Holerites D", null, null);
        var condominiumB = new Condominium("Condo Holerites E", null, null);
        var manager = CoreTestSeed.User("Síndico Multi Holerites", "sindico-multi-holerites@test.local");
        await _host.WithDbAsync(async db =>
        {
            db.AddRange(condominiumA, condominiumB, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominiumA.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(db, manager.Id, condominiumB.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominiumA.Id, DateTime.UtcNow);
            CondominiumModuleService.AddDefaults(db, condominiumB.Id, DateTime.UtcNow);
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(_host, condominiumA.Id);
        await EnableModuleAsync(_host, condominiumB.Id);
        var client = _host.ClientFor(manager.Id);

        var pdf = BuildPdf("Recibo de pagamento sem identificacao de funcionario.");
        var (_, batchId) = await UploadAsync(client, condominiumA.Id, pdf);
        await ProcessAsync(batchId);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/condominiums/{condominiumA.Id}/employees/documents/batches/{batchId}");
        var documentId = detail.GetProperty("documents")[0].GetProperty("id").GetGuid();

        var crossBatch = await client.GetAsync($"/condominiums/{condominiumB.Id}/employees/documents/batches/{batchId}");
        Assert.Equal(HttpStatusCode.NotFound, crossBatch.StatusCode);
        var crossPreview = await client.GetAsync($"/condominiums/{condominiumB.Id}/employees/documents/{documentId}/preview");
        Assert.Equal(HttpStatusCode.NotFound, crossPreview.StatusCode);
        var crossPatch = await client.PatchAsJsonAsync($"/condominiums/{condominiumB.Id}/employees/documents/{documentId}",
            new UpdateEmployeeDocumentAssociationRequest("Ignore", null));
        Assert.Equal(HttpStatusCode.NotFound, crossPatch.StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_invalid_files_without_creating_a_partial_batch()
    {
        var condominium = new Condominium("Condo Holerites Upload", null, null);
        var manager = CoreTestSeed.User("Síndico Upload", "sindico-upload@test.local");
        await _host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(_host, condominium.Id);
        var client = _host.ClientFor(manager.Id);

        var validPdf = BuildPdf("Recibo de pagamento valido para o teste.");

        var emptyFile = await UploadFilesAsync(client, condominium.Id, [("vazio.pdf", [])], expectSuccess: false);
        Assert.Equal(HttpStatusCode.BadRequest, emptyFile.Response.StatusCode);

        var corrupted = await UploadFilesAsync(client, condominium.Id,
            [("corrompido.pdf", "isto nao e um pdf de verdade"u8.ToArray())], expectSuccess: false);
        Assert.Equal(HttpStatusCode.BadRequest, corrupted.Response.StatusCode);

        var wrongExtension = await UploadFilesAsync(client, condominium.Id, [("planilha.xlsx", validPdf)], expectSuccess: false);
        Assert.Equal(HttpStatusCode.BadRequest, wrongExtension.Response.StatusCode);

        // One corrupted file among otherwise-valid ones must reject the whole
        // batch — never silently process only the valid subset.
        var mixed = await UploadFilesAsync(client, condominium.Id,
            [("folha1.pdf", validPdf), ("folha2.pdf", [1, 2, 3])], expectSuccess: false);
        Assert.Equal(HttpStatusCode.BadRequest, mixed.Response.StatusCode);

        var tooMany = await UploadFilesAsync(client, condominium.Id,
            Enumerable.Range(0, UploadEmployeeDocumentBatch.MaximumFileCount + 1)
                .Select(i => ($"folha{i}.pdf", validPdf)).ToArray(), expectSuccess: false);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.Response.StatusCode);

        var batchCount = await _host.WithDbAsync(db => db.EmployeeDocumentBatches.CountAsync(x => x.CondominiumId == condominium.Id));
        Assert.Equal(0, batchCount); // nothing was created by any of the rejected uploads.

        var valid = await UploadFilesAsync(client, condominium.Id, [("folha_valida.pdf", validPdf)], expectSuccess: true);
        Assert.Equal(HttpStatusCode.Accepted, valid.Response.StatusCode);
    }

    [Fact]
    public async Task SubManager_without_EmployeeManagement_permission_cannot_upload()
    {
        var condominium = new Condominium("Condo Holerites F", null, null);
        var subManager = CoreTestSeed.User("Subsíndico Holerites", "sub-holerites@test.local");
        await _host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, subManager);
            CoreTestSeed.AddMember(db, subManager.Id, condominium.Id, CondominiumRole.SubManager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(_host, condominium.Id);
        var client = _host.ClientFor(subManager.Id);

        var response = await client.GetAsync($"/condominiums/{condominium.Id}/employees/documents/batches");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Management_company_employee_can_operate_full_flow_without_a_manager()
    {
        var condominium = new Condominium("Condo Holerites Sem Sindico", null, null);
        var company = new ManagementCompany("Administradora Holerites", null, null, null, null);
        var operatorUser = CoreTestSeed.User("Operador DP Holerites", "dp-holerites@test.local");
        Guid employeeId = Guid.Empty;
        await _host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, company, operatorUser);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            var employee = new Employee(condominium.Id, "Funcionário Administradora", null, null, null, "MAT-500", null);
            db.Add(employee);
            await db.SaveChangesAsync();
            employeeId = employee.Id;
            condominium.SetManagementCompany(company.Id);
            var companyEmployee = new ManagementCompanyEmployee(company.Id, operatorUser.Id, "Departamento Pessoal");
            db.Add(companyEmployee);
            db.Add(new ManagementCompanyEmployeeModulePermission(companyEmployee.Id, CondominiumModuleType.EmployeeManagement, operatorUser.Id));
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(_host, condominium.Id, managementCompanyAccessEnabled: true);
        var hasAnyMembership = await _host.WithDbAsync(db => Task.FromResult(db.CondominiumMemberships.Any(x => x.CondominiumId == condominium.Id)));
        Assert.False(hasAnyMembership);
        var client = _host.ClientFor(operatorUser.Id);

        var pdf = BuildPdf("RECIBO DE PAGAMENTO\nMatricula: MAT-500");
        var (_, batchId) = await UploadAsync(client, condominium.Id, pdf);
        await ProcessAsync(batchId);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}");
        var documentId = detail.GetProperty("documents")[0].GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync(
            $"/condominiums/{condominium.Id}/employees/documents/{documentId}",
            new UpdateEmployeeDocumentAssociationRequest("Confirm", null))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(
            $"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/confirm", null)).StatusCode);

        var summary = await client.GetFromJsonAsync<EmployeeDocumentDistributionSummaryResponse>(
            $"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/distribution-summary");
        Assert.Equal(0, summary!.Ready); // employee has no phone number in this test.

        await _host.WithDbAsync(async db =>
        {
            var employee = await db.Employees.SingleAsync(x => x.Id == employeeId);
            employee.Update(employee.FullName, employee.JobTitle, "11987654321", null, employee.RegistrationNumber, null);
            await db.SaveChangesAsync();
        });
        var distribute = await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/distribute", null);
        Assert.Equal(HttpStatusCode.OK, distribute.StatusCode);
        var distributeBody = await distribute.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, distributeBody.GetProperty("queued").GetInt32());

        await DispatchWhatsAppAsync();
        Assert.Single(_whatsAppClient.TemplateSends);
        Assert.NotNull(_whatsAppClient.TemplateSends[0].DocumentHeader);

        // A second distribute call must not queue the same document twice (idempotency).
        var secondDistribute = await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/distribute", null);
        Assert.Equal(HttpStatusCode.OK, secondDistribute.StatusCode);
        var deliveries = await _host.WithDbAsync(db => Task.FromResult(
            db.EmployeeDocumentDeliveries.Count(x => x.EmployeeDocumentId == documentId)));
        Assert.Equal(1, deliveries);

        // The administradora-without-a-Manager flow must also reach Completed
        // once its (only) delivery goes terminal — no dependency on a síndico
        // existing anywhere in this pipeline.
        var finalBatch = await _host.WithDbAsync(db => db.EmployeeDocumentBatches.AsNoTracking().SingleAsync(x => x.Id == batchId));
        Assert.Equal(EmployeeDocumentBatchStatus.Completed, finalBatch.Status);
    }

    [Fact]
    public async Task Employee_deactivated_after_queueing_blocks_the_send_at_dispatch_time()
    {
        var condominium = new Condominium("Condo Holerites G", null, null);
        var manager = CoreTestSeed.User("Síndico G", "sindico-holerites-g@test.local");
        Guid employeeId = Guid.Empty;
        await _host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            var employee = new Employee(condominium.Id, "Funcionário G", null, "11987654321", null, "MAT-900", null);
            db.Add(employee);
            await db.SaveChangesAsync();
            employeeId = employee.Id;
        });
        await EnableModuleAsync(_host, condominium.Id);
        var client = _host.ClientFor(manager.Id);

        var pdf = BuildPdf("RECIBO DE PAGAMENTO\nMatricula: MAT-900");
        var (_, batchId) = await UploadAsync(client, condominium.Id, pdf);
        await ProcessAsync(batchId);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}");
        var documentId = detail.GetProperty("documents")[0].GetProperty("id").GetGuid();
        await client.PatchAsJsonAsync($"/condominiums/{condominium.Id}/employees/documents/{documentId}",
            new UpdateEmployeeDocumentAssociationRequest("Confirm", null));
        await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/confirm", null);
        await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/distribute", null);

        // Deactivated between queueing and the worker actually attempting the send.
        await _host.WithDbAsync(async db =>
        {
            var employee = await db.Employees.SingleAsync(x => x.Id == employeeId);
            employee.Deactivate();
            await db.SaveChangesAsync();
        });

        await DispatchWhatsAppAsync();
        Assert.Empty(_whatsAppClient.TemplateSends);
        var outbound = await _host.WithDbAsync(db => db.WhatsAppOutboundMessages.AsNoTracking()
            .SingleAsync(x => x.EmployeeDocumentId == documentId));
        Assert.Equal(WhatsAppOutboundStatus.PermanentlyFailed, outbound.Status);
        Assert.Equal("employee_inactive", outbound.LastErrorCode);
    }

    [Fact]
    public async Task Resend_is_rejected_when_no_delivery_has_failed()
    {
        var condominium = new Condominium("Condo Holerites H", null, null);
        var manager = CoreTestSeed.User("Síndico H", "sindico-holerites-h@test.local");
        await _host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            db.Add(new Employee(condominium.Id, "Funcionário H", null, "11987654321", null, "MAT-901", null));
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(_host, condominium.Id);
        var client = _host.ClientFor(manager.Id);

        var pdf = BuildPdf("RECIBO DE PAGAMENTO\nMatricula: MAT-901");
        var (_, batchId) = await UploadAsync(client, condominium.Id, pdf);
        await ProcessAsync(batchId);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}");
        var documentId = detail.GetProperty("documents")[0].GetProperty("id").GetGuid();
        await client.PatchAsJsonAsync($"/condominiums/{condominium.Id}/employees/documents/{documentId}",
            new UpdateEmployeeDocumentAssociationRequest("Confirm", null));
        await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/confirm", null);
        await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/distribute", null);
        await DispatchWhatsAppAsync();
        Assert.Single(_whatsAppClient.TemplateSends); // succeeded — nothing to retry.

        var resend = await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/{documentId}/resend", null);
        Assert.Equal(HttpStatusCode.Conflict, resend.StatusCode);
    }

    [Fact]
    public async Task Reimporting_the_same_confirmed_payslip_is_flagged_as_a_possible_duplicate()
    {
        var condominium = new Condominium("Condo Holerites I", null, null);
        var manager = CoreTestSeed.User("Síndico I", "sindico-holerites-i@test.local");
        await _host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            db.Add(new Employee(condominium.Id, "Funcionário I", null, null, null, "MAT-910", null));
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(_host, condominium.Id);
        var client = _host.ClientFor(manager.Id);

        var pdf = BuildPdf("RECIBO DE PAGAMENTO\nMatricula: MAT-910");

        // First import: confirm it.
        var (_, firstBatchId) = await UploadAsync(client, condominium.Id, pdf);
        await ProcessAsync(firstBatchId);
        var firstDetail = await client.GetFromJsonAsync<JsonElement>($"/condominiums/{condominium.Id}/employees/documents/batches/{firstBatchId}");
        var firstDocumentId = firstDetail.GetProperty("documents")[0].GetProperty("id").GetGuid();
        Assert.False(firstDetail.GetProperty("documents")[0].GetProperty("possibleDuplicate").GetBoolean());
        await client.PatchAsJsonAsync($"/condominiums/{condominium.Id}/employees/documents/{firstDocumentId}",
            new UpdateEmployeeDocumentAssociationRequest("Confirm", null));
        await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{firstBatchId}/confirm", null);

        // Second import of the exact same rendered PDF, same employee, same competence.
        var (_, secondBatchId) = await UploadAsync(client, condominium.Id, pdf);
        await ProcessAsync(secondBatchId);
        var secondDetail = await client.GetFromJsonAsync<JsonElement>($"/condominiums/{condominium.Id}/employees/documents/batches/{secondBatchId}");
        Assert.True(secondDetail.GetProperty("documents")[0].GetProperty("possibleDuplicate").GetBoolean());

        // Duplicate detection warns but never blocks the legitimate re-import/correction.
        var secondDocumentId = secondDetail.GetProperty("documents")[0].GetProperty("id").GetGuid();
        var confirmSecond = await client.PatchAsJsonAsync($"/condominiums/{condominium.Id}/employees/documents/{secondDocumentId}",
            new UpdateEmployeeDocumentAssociationRequest("Confirm", null));
        Assert.Equal(HttpStatusCode.OK, confirmSecond.StatusCode);
    }

    [Fact]
    public async Task Same_content_hash_for_a_different_employee_is_not_flagged_as_duplicate()
    {
        var condominium = new Condominium("Condo Holerites J", null, null);
        var manager = CoreTestSeed.User("Síndico J", "sindico-holerites-j@test.local");
        await _host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            db.Add(new Employee(condominium.Id, "Funcionário J1", null, null, null, "MAT-920", null));
            db.Add(new Employee(condominium.Id, "Funcionário J2", null, null, null, "MAT-921", null));
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(_host, condominium.Id);
        var client = _host.ClientFor(manager.Id);

        // A blank/templated page with no distinguishing content could hash the
        // same for two different employees — that must never cross-flag them.
        var genericPdf = BuildPdf("Documento genérico sem identificação");
        var (_, batch1Id) = await UploadAsync(client, condominium.Id, genericPdf);
        await ProcessAsync(batch1Id);
        var detail1 = await client.GetFromJsonAsync<JsonElement>($"/condominiums/{condominium.Id}/employees/documents/batches/{batch1Id}");
        var document1Id = detail1.GetProperty("documents")[0].GetProperty("id").GetGuid();
        var employee1Id = (await _host.WithDbAsync(db => db.Employees.SingleAsync(x => x.RegistrationNumber == "MAT-920"))).Id;
        await client.PatchAsJsonAsync($"/condominiums/{condominium.Id}/employees/documents/{document1Id}",
            new UpdateEmployeeDocumentAssociationRequest("Assign", employee1Id));
        await client.PatchAsJsonAsync($"/condominiums/{condominium.Id}/employees/documents/{document1Id}",
            new UpdateEmployeeDocumentAssociationRequest("Confirm", null));
        await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batch1Id}/confirm", null);

        var (_, batch2Id) = await UploadAsync(client, condominium.Id, genericPdf);
        await ProcessAsync(batch2Id);
        var detail2 = await client.GetFromJsonAsync<JsonElement>($"/condominiums/{condominium.Id}/employees/documents/batches/{batch2Id}");
        var document2Id = detail2.GetProperty("documents")[0].GetProperty("id").GetGuid();
        var employee2Id = (await _host.WithDbAsync(db => db.Employees.SingleAsync(x => x.RegistrationNumber == "MAT-921"))).Id;
        await client.PatchAsJsonAsync($"/condominiums/{condominium.Id}/employees/documents/{document2Id}",
            new UpdateEmployeeDocumentAssociationRequest("Assign", employee2Id));
        var afterAssign = await (await client.GetAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batch2Id}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(afterAssign.GetProperty("documents")[0].GetProperty("possibleDuplicate").GetBoolean());
    }

    [Fact]
    public async Task A_genuinely_different_document_for_the_same_employee_and_competence_is_not_flagged()
    {
        var condominium = new Condominium("Condo Holerites K", null, null);
        var manager = CoreTestSeed.User("Síndico K", "sindico-holerites-k@test.local");
        await _host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            db.Add(new Employee(condominium.Id, "Funcionário K", null, null, null, "MAT-930", null));
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(_host, condominium.Id);
        var client = _host.ClientFor(manager.Id);

        var (_, firstBatchId) = await UploadAsync(client, condominium.Id, BuildPdf("RECIBO DE PAGAMENTO\nMatricula: MAT-930\nValor: 1000"));
        await ProcessAsync(firstBatchId);
        var firstDetail = await client.GetFromJsonAsync<JsonElement>($"/condominiums/{condominium.Id}/employees/documents/batches/{firstBatchId}");
        var firstDocumentId = firstDetail.GetProperty("documents")[0].GetProperty("id").GetGuid();
        await client.PatchAsJsonAsync($"/condominiums/{condominium.Id}/employees/documents/{firstDocumentId}",
            new UpdateEmployeeDocumentAssociationRequest("Confirm", null));
        await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{firstBatchId}/confirm", null);

        // Same employee, same competence, but a genuinely different rendered document (corrected value).
        var (_, secondBatchId) = await UploadAsync(client, condominium.Id, BuildPdf("RECIBO DE PAGAMENTO\nMatricula: MAT-930\nValor: 1200 (corrigido)"));
        await ProcessAsync(secondBatchId);
        var secondDetail = await client.GetFromJsonAsync<JsonElement>($"/condominiums/{condominium.Id}/employees/documents/batches/{secondBatchId}");
        Assert.False(secondDetail.GetProperty("documents")[0].GetProperty("possibleDuplicate").GetBoolean());
    }

    [Fact]
    public async Task A_batch_stuck_in_Processing_is_recovered_and_reprocessed_by_the_worker()
    {
        var condominium = new Condominium("Condo Holerites L", null, null);
        var manager = CoreTestSeed.User("Síndico L", "sindico-holerites-l@test.local");
        await _host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(_host, condominium.Id);
        var client = _host.ClientFor(manager.Id);

        var pdf = BuildPdf("Recibo de pagamento sem identificacao de funcionario.");
        var (_, batchId) = await UploadAsync(client, condominium.Id, pdf);

        // Simulate a worker that crashed right after claiming the batch: stuck in
        // Processing, created long enough ago to be considered abandoned.
        await _host.WithDbAsync(async db =>
        {
            var batch = await db.EmployeeDocumentBatches.SingleAsync(x => x.Id == batchId);
            batch.StartProcessing();
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE employee_document_batches SET created_at = {DateTime.UtcNow.AddMinutes(-30)} WHERE id = {batchId}");
        });

        await _host.WithServicesAsync(async services =>
        {
            var worker = services.GetRequiredService<EmployeeDocumentProcessingWorker>();
            await worker.ProcessBatchAsync(default);
        });

        var reprocessed = await _host.WithDbAsync(db => db.EmployeeDocumentBatches.AsNoTracking().SingleAsync(x => x.Id == batchId));
        Assert.Equal(EmployeeDocumentBatchStatus.ReadyForReview, reprocessed.Status);
    }

    private async Task<(Condominium Condominium, Employee Employee1, Employee Employee2, HttpClient Client,
        Guid BatchId, Guid Document1Id, Guid Document2Id)> SeedTwoEmployeeConfirmedBatchAsync(string suffix)
    {
        var condominium = new Condominium($"Condo Completion {suffix}", null, null);
        var manager = CoreTestSeed.User($"Síndico {suffix}", $"sindico-completion-{suffix}@test.local");
        Employee employee1 = null!, employee2 = null!;
        await _host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            employee1 = new Employee(condominium.Id, $"Funcionário Um {suffix}", null, "11900000001", null, $"MAT-{suffix}1", null);
            employee2 = new Employee(condominium.Id, $"Funcionário Dois {suffix}", null, "11900000002", null, $"MAT-{suffix}2", null);
            db.AddRange(employee1, employee2);
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(_host, condominium.Id);
        var client = _host.ClientFor(manager.Id);

        var pdf = BuildPdf($"RECIBO DE PAGAMENTO\nMatricula: MAT-{suffix}1", $"RECIBO DE PAGAMENTO\nMatricula: MAT-{suffix}2");
        var (_, batchId) = await UploadAsync(client, condominium.Id, pdf);
        await ProcessAsync(batchId);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}");
        var documents = detail.GetProperty("documents").EnumerateArray().ToArray();
        var document1Id = documents[0].GetProperty("id").GetGuid();
        var document2Id = documents[1].GetProperty("id").GetGuid();
        await client.PatchAsJsonAsync($"/condominiums/{condominium.Id}/employees/documents/{document1Id}",
            new UpdateEmployeeDocumentAssociationRequest("Confirm", null));
        await client.PatchAsJsonAsync($"/condominiums/{condominium.Id}/employees/documents/{document2Id}",
            new UpdateEmployeeDocumentAssociationRequest("Confirm", null));
        await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/confirm", null);
        return (condominium, employee1, employee2, client, batchId, document1Id, document2Id);
    }

    private async Task<EmployeeDocumentBatchStatus> GetBatchStatusAsync(Guid batchId) =>
        (await _host.WithDbAsync(db => db.EmployeeDocumentBatches.AsNoTracking().SingleAsync(x => x.Id == batchId))).Status;

    [Fact]
    public async Task All_deliveries_reaching_a_terminal_status_completes_the_batch()
    {
        var (condominium, _, _, client, batchId, _, _) = await SeedTwoEmployeeConfirmedBatchAsync("B1");
        await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/distribute", null);
        await DispatchWhatsAppAsync();
        Assert.Equal(EmployeeDocumentBatchStatus.Completed, await GetBatchStatusAsync(batchId));
    }

    [Fact]
    public async Task A_delivery_still_pending_keeps_the_batch_in_Distributing()
    {
        var (condominium, _, _, client, batchId, _, document2Id) = await SeedTwoEmployeeConfirmedBatchAsync("B2");
        await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/distribute", null);

        // Push document 2's send far into the future so only document 1 is
        // actually processed by this dispatch — document 2 stays genuinely Pending.
        await _host.WithDbAsync(async db =>
        {
            var outboundId = await db.EmployeeDocumentDeliveries.AsNoTracking()
                .Where(x => x.EmployeeDocumentId == document2Id).Select(x => x.OutboundMessageId).SingleAsync();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE whatsapp_outbound_messages SET next_attempt_at = {DateTime.UtcNow.AddHours(1)} WHERE id = {outboundId}");
        });

        await DispatchWhatsAppAsync();
        Assert.Equal(EmployeeDocumentBatchStatus.Distributing, await GetBatchStatusAsync(batchId));
    }

    [Fact]
    public async Task A_permanent_failure_alongside_successes_still_completes_the_batch()
    {
        var (condominium, _, employee2, client, batchId, _, _) = await SeedTwoEmployeeConfirmedBatchAsync("B3");
        _whatsAppClient.PermanentFailurePhones.Add(employee2.NormalizedPhoneNumber!);

        await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/distribute", null);
        await DispatchWhatsAppAsync();

        Assert.Equal(EmployeeDocumentBatchStatus.Completed, await GetBatchStatusAsync(batchId));
        var statuses = await _host.WithDbAsync(db => db.WhatsAppOutboundMessages.AsNoTracking()
            .Where(x => x.CondominiumId == condominium.Id).Select(x => x.Status).ToListAsync());
        Assert.Contains(WhatsAppOutboundStatus.Sent, statuses);
        Assert.Contains(WhatsAppOutboundStatus.PermanentlyFailed, statuses);
    }

    [Fact]
    public async Task A_pending_automatic_retry_prevents_completion_until_it_resolves()
    {
        var (condominium, _, employee2, client, batchId, _, _) = await SeedTwoEmployeeConfirmedBatchAsync("B4");
        _whatsAppClient.TransientFailurePhones.Add(employee2.NormalizedPhoneNumber!);

        await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/distribute", null);
        await DispatchWhatsAppAsync();

        // Employee 2's message was rescheduled (still Pending, future NextAttemptAt) —
        // the automatic retry hasn't had its chance yet, so the batch must not complete.
        Assert.Equal(EmployeeDocumentBatchStatus.Distributing, await GetBatchStatusAsync(batchId));
        var employee2Status = await _host.WithDbAsync(db => db.WhatsAppOutboundMessages.AsNoTracking()
            .Where(x => x.DestinationPhone == employee2.NormalizedPhoneNumber).Select(x => x.Status).SingleAsync());
        Assert.Equal(WhatsAppOutboundStatus.Pending, employee2Status);
    }

    [Fact]
    public async Task Failure_before_reaching_meta_still_allows_completion_once_the_rest_are_terminal()
    {
        var (condominium, _, employee2, client, batchId, _, _) = await SeedTwoEmployeeConfirmedBatchAsync("B5");
        await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/distribute", null);

        // Deactivated after queueing: the worker rejects this one before ever
        // calling UploadDocumentAsync/SendTemplateAsync — no ExternalMessageId,
        // so the webhook will never hear about it either.
        await _host.WithDbAsync(async db =>
        {
            var employee = await db.Employees.SingleAsync(x => x.Id == employee2.Id);
            employee.Deactivate();
            await db.SaveChangesAsync();
        });

        await DispatchWhatsAppAsync();
        Assert.Equal(EmployeeDocumentBatchStatus.Completed, await GetBatchStatusAsync(batchId));
    }

    [Fact]
    public async Task Resend_after_completion_returns_to_Distributing_then_completes_again_preserving_history()
    {
        var (condominium, _, employee2, client, batchId, _, document2Id) = await SeedTwoEmployeeConfirmedBatchAsync("B6");
        _whatsAppClient.PermanentFailurePhones.Add(employee2.NormalizedPhoneNumber!);

        await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/distribute", null);
        await DispatchWhatsAppAsync();
        Assert.Equal(EmployeeDocumentBatchStatus.Completed, await GetBatchStatusAsync(batchId));

        var resend = await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/{document2Id}/resend", null);
        Assert.Equal(HttpStatusCode.NoContent, resend.StatusCode);
        Assert.Equal(EmployeeDocumentBatchStatus.Distributing, await GetBatchStatusAsync(batchId));

        await DispatchWhatsAppAsync(); // no failure configured this time around — the retry succeeds.
        Assert.Equal(EmployeeDocumentBatchStatus.Completed, await GetBatchStatusAsync(batchId));

        // The failed attempt's row is never deleted or overwritten — it's history.
        var outboundCount = await _host.WithDbAsync(db => db.WhatsAppOutboundMessages.CountAsync(x => x.EmployeeDocumentId == document2Id));
        Assert.Equal(2, outboundCount);
        var permanentlyFailedStillRecorded = await _host.WithDbAsync(db => db.WhatsAppOutboundMessages.AsNoTracking()
            .AnyAsync(x => x.EmployeeDocumentId == document2Id && x.Status == WhatsAppOutboundStatus.PermanentlyFailed));
        Assert.True(permanentlyFailedStillRecorded);
    }

    [Fact]
    public async Task TryCompleteBatchAsync_is_idempotent()
    {
        var (condominium, _, _, client, batchId, _, _) = await SeedTwoEmployeeConfirmedBatchAsync("B7");
        await client.PostAsync($"/condominiums/{condominium.Id}/employees/documents/batches/{batchId}/distribute", null);
        await DispatchWhatsAppAsync();
        Assert.Equal(EmployeeDocumentBatchStatus.Completed, await GetBatchStatusAsync(batchId));

        var secondCallResult = await _host.WithServicesAsync(services =>
            services.GetRequiredService<EmployeeDocumentDistributionService>().TryCompleteBatchAsync(batchId, default));
        Assert.False(secondCallResult); // already Completed — no-op, no error.
        Assert.Equal(EmployeeDocumentBatchStatus.Completed, await GetBatchStatusAsync(batchId));
    }

    private sealed class PassthroughVerificationProtector : IPhoneVerificationMessageProtector
    {
        public string Protect(string message) => message;
        public string Unprotect(string protectedMessage) => protectedMessage;
    }

    private sealed class UnusedFirstAccessProtector : IFirstAccessWhatsAppPayloadProtector
    {
        public string Protect(FirstAccessWhatsAppPayload payload) => throw new NotSupportedException();
        public FirstAccessWhatsAppPayload Unprotect(string value) => throw new NotSupportedException();
    }

    private sealed class DisabledOcrService : IDocumentOcrService
    {
        public bool Enabled => false;
        public Task<string?> ExtractTextAsync(byte[] imageBytes, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }

    internal sealed class CapturingWhatsAppClient : IWhatsAppClient
    {
        public List<(string Phone, string Template, WhatsAppDocumentHeader? DocumentHeader)> TemplateSends { get; } = [];

        // Test hook: phones in either set fail their next SendTemplateAsync call
        // instead of succeeding — transient (worker reschedules to Pending) or
        // permanent (no further automatic retry) — to exercise those paths.
        public HashSet<string> TransientFailurePhones { get; } = [];
        public HashSet<string> PermanentFailurePhones { get; } = [];

        public Task<WhatsAppSendResult> SendTextAsync(string phoneNumber, string text, CancellationToken cancellationToken) =>
            Task.FromResult(new WhatsAppSendResult(true, Guid.NewGuid().ToString("N"), null));

        public Task<WhatsAppMediaResult> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken) =>
            Task.FromResult(new WhatsAppMediaResult(false, null, null, "not supported"));

        public Task<WhatsAppSendResult> SendTemplateAsync(string phoneNumber, string templateName, string language,
            IReadOnlyList<string> bodyParameters, IReadOnlyList<string> quickReplyPayloads, CancellationToken cancellationToken,
            string? bodyParameterName = null) =>
            SendTemplateAsync(phoneNumber, templateName, language, bodyParameters, quickReplyPayloads, cancellationToken,
                bodyParameterName, [], null);

        public Task<WhatsAppSendResult> SendTemplateAsync(string phoneNumber, string templateName, string language,
            IReadOnlyList<string> bodyParameters, IReadOnlyList<string> quickReplyPayloads, CancellationToken cancellationToken,
            string? bodyParameterName, IReadOnlyList<string> urlButtonParameters, WhatsAppDocumentHeader? documentHeader)
        {
            TemplateSends.Add((phoneNumber, templateName, documentHeader));
            if (TransientFailurePhones.Remove(phoneNumber))
                return Task.FromResult(new WhatsAppSendResult(false, null, "Simulated transient failure.", true, "simulated_transient"));
            if (PermanentFailurePhones.Remove(phoneNumber))
                return Task.FromResult(new WhatsAppSendResult(false, null, "Simulated permanent failure.", false, "simulated_permanent"));
            return Task.FromResult(new WhatsAppSendResult(true, Guid.NewGuid().ToString("N"), null));
        }

        public Task<WhatsAppMediaUploadResult> UploadDocumentAsync(byte[] content, string fileName, string mimeType,
            CancellationToken cancellationToken) => Task.FromResult(new WhatsAppMediaUploadResult(true, Guid.NewGuid().ToString("N"), null));
    }
}

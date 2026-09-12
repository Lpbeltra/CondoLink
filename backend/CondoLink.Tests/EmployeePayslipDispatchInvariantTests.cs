using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Api.Features.CondominiumModules;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace CondoLink.Tests;

/// <summary>
/// Direct, database-level tests of <c>WhatsAppOutboundWorker.PrepareEmployeePayslipSendAsync</c>
/// — the single most important safety net in the whole payslip feature: "a
/// employee's payslip must never be delivered to another employee." These
/// tests deliberately construct data states an endpoint bug or a data
/// corruption could produce (mismatched CondominiumId, an employee from a
/// different condominium) and assert the send-time revalidation catches them
/// before any Meta API call — plus a byte-level proof that two employees'
/// individualized PDFs are never swapped when their sends are prepared back
/// to back against the same shared db/client/storage instances, mirroring how
/// the real worker's sequential per-item loop reuses them.
/// </summary>
public sealed class EmployeePayslipDispatchInvariantTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "condolink-payslip-dispatch-tests", Guid.NewGuid().ToString("N"));
    private AppDbContext _db = null!;
    private LocalFileStorage _storage = null!;
    private RecordingWhatsAppClient _client = null!;
    private ICondominiumModuleService _modules = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _storage = new LocalFileStorage(new FakeConfiguration(_storageRoot), new FakeWebHostEnvironment());
        _client = new RecordingWhatsAppClient();
        _modules = new CondominiumModuleService(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
        if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, true);
    }

    private async Task<(Condominium Condominium, Employee Employee)> SeedCondominiumWithEmployeeAsync(
        string condominiumName, string employeeName, string phone)
    {
        var condominium = new Condominium(condominiumName, null, null);
        var employee = new Employee(condominium.Id, employeeName, null, phone, null, null, null);
        _db.AddRange(condominium, employee);
        CondominiumModuleService.AddDefaults(_db, condominium.Id, DateTime.UtcNow);
        await _db.SaveChangesAsync();
        var moduleRow = await _db.CondominiumModules.SingleAsync(x => x.CondominiumId == condominium.Id && x.Module == CondominiumModuleType.EmployeeManagement);
        moduleRow.Set(true, false, DateTime.UtcNow);
        await _db.SaveChangesAsync();
        return (condominium, employee);
    }

    private async Task<(EmployeeDocumentBatch Batch, EmployeeDocument Document, WhatsAppOutboundMessage Outbound)>
        SeedConfirmedDocumentAsync(Guid condominiumId, Employee employee, string pdfMarker)
    {
        var actor = CoreTestSeed.User("Operador", $"operador-{Guid.NewGuid():N}@test.local");
        _db.Add(actor);
        await _db.SaveChangesAsync();

        var batch = new EmployeeDocumentBatch(condominiumId, EmployeeDocumentType.Payslip, 8, 2026, actor.Id, DateTime.UtcNow);
        batch.StartProcessing();
        batch.MarkReadyForReview();
        var pdfBytes = BuildMarkedPdf(pdfMarker);
        var document = new EmployeeDocument(condominiumId, batch.Id, EmployeeDocumentType.Payslip, 8, 2026,
            "pending", "folha.pdf", 1, 1, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdfBytes)), DateTime.UtcNow);
        var fileKey = await _storage.SaveEmployeeDocumentAsync(condominiumId, batch.Id, document.Id, pdfBytes, default);
        document.SetFileKey(fileKey);
        document.ApplyAutomaticIdentification(employee.Id, EmployeeDocumentIdentificationConfidence.High, EmployeeDocumentIdentificationMethod.RegistrationNumber, DateTime.UtcNow);
        document.Confirm(DateTime.UtcNow);
        batch.Confirm(actor.Id, DateTime.UtcNow);
        _db.AddRange(batch, document);
        await _db.SaveChangesAsync();

        var outbound = new WhatsAppOutboundMessage(null, null, actor.Id, condominiumId, employee.NormalizedPhoneNumber!,
            WhatsAppNotificationType.EmployeePayslipAvailable, WhatsAppSendMode.Template,
            $"employee-document:{document.Id}:whatsapp:1", "Holerite disponível.", "holerite_disponivel", "pt_BR",
            DateTime.UtcNow, employeeDocumentId: document.Id);
        _db.Add(outbound);
        await _db.SaveChangesAsync();
        return (batch, document, outbound);
    }

    private static byte[] BuildMarkedPdf(string marker)
    {
        GlobalFontSettings.FontResolver ??= new CondoLink.Api.Features.CondominiumMembers.ComvyFontResolver();
        using var document = new PdfDocument();
        var page = document.AddPage();
        using var graphics = XGraphics.FromPdfPage(page);
        graphics.DrawString(marker, new XFont("ComvySans", 12), XBrushes.Black, new XPoint(40, 60));
        using var buffer = new MemoryStream();
        document.Save(buffer, false);
        return buffer.ToArray();
    }

    private static string ExtractText(byte[] pdfBytes)
    {
        using var stream = new MemoryStream(pdfBytes);
        return CondominiumDocumentText.Extract(stream, ".pdf");
    }

    [Fact]
    public async Task Employee_from_a_different_condominium_than_the_document_aborts_without_calling_meta()
    {
        var (condominiumA, _) = await SeedCondominiumWithEmployeeAsync("Condo A", "Funcionário A", "11987654321");
        var (_, employeeB) = await SeedCondominiumWithEmployeeAsync("Condo B", "Funcionário B", "11987654322");

        // Simulate a data corruption / bypassed validation: the document lives in
        // condominium A but its EmployeeId points at an employee from condominium B.
        var (_, _, outbound) = await SeedConfirmedDocumentAsync(condominiumA.Id, employeeB, "SHOULD_NOT_BE_SENT");

        var (parameters, header, failure) = await WhatsAppOutboundWorker.PrepareEmployeePayslipSendAsync(
            outbound, _db, _client, _storage, _modules, default);

        Assert.NotNull(failure);
        Assert.Equal("employee_document_condominium_mismatch", failure!.ErrorCode);
        Assert.False(failure.Succeeded);
        Assert.Empty(parameters);
        Assert.Null(header);
        Assert.Empty(_client.UploadCalls); // Meta was never contacted.
        Assert.Empty(_client.TemplateSends);
    }

    [Fact]
    public async Task Two_employees_dispatched_back_to_back_never_receive_each_others_pdf()
    {
        var (condominium, employeeA) = await SeedCondominiumWithEmployeeAsync("Condo Compartilhado", "Ana Empregada", "11987654321");
        var employeeB = new Employee(condominium.Id, "Bruno Empregado", null, "11987654322", null, null, null);
        _db.Add(employeeB);
        await _db.SaveChangesAsync();

        var (_, documentA, outboundA) = await SeedConfirmedDocumentAsync(condominium.Id, employeeA, "MARKER_EMPLOYEE_A_ONLY");
        var (_, documentB, outboundB) = await SeedConfirmedDocumentAsync(condominium.Id, employeeB, "MARKER_EMPLOYEE_B_ONLY");

        // Prepared sequentially against the SAME db/client/storage instances —
        // exactly how the real worker's per-item loop reuses them.
        var (_, headerA, failureA) = await WhatsAppOutboundWorker.PrepareEmployeePayslipSendAsync(
            outboundA, _db, _client, _storage, _modules, default);
        var (_, headerB, failureB) = await WhatsAppOutboundWorker.PrepareEmployeePayslipSendAsync(
            outboundB, _db, _client, _storage, _modules, default);

        Assert.Null(failureA);
        Assert.Null(failureB);
        Assert.Equal(2, _client.UploadCalls.Count);

        var uploadedForA = _client.UploadCalls[headerA!.MediaId];
        var uploadedForB = _client.UploadCalls[headerB!.MediaId];
        var textForA = ExtractText(uploadedForA);
        var textForB = ExtractText(uploadedForB);

        Assert.Contains("MARKER_EMPLOYEE_A_ONLY", textForA);
        Assert.DoesNotContain("MARKER_EMPLOYEE_B_ONLY", textForA);
        Assert.Contains("MARKER_EMPLOYEE_B_ONLY", textForB);
        Assert.DoesNotContain("MARKER_EMPLOYEE_A_ONLY", textForB);
    }

    private sealed class RecordingWhatsAppClient : IWhatsAppClient
    {
        public Dictionary<string, byte[]> UploadCalls { get; } = [];
        public List<(string Phone, WhatsAppDocumentHeader? Header)> TemplateSends { get; } = [];
        private int _mediaCounter;

        public Task<WhatsAppSendResult> SendTextAsync(string phoneNumber, string text, CancellationToken cancellationToken) =>
            Task.FromResult(new WhatsAppSendResult(true, Guid.NewGuid().ToString("N"), null));

        public Task<WhatsAppMediaResult> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken) =>
            Task.FromResult(new WhatsAppMediaResult(false, null, null, "not supported"));

        public Task<WhatsAppSendResult> SendTemplateAsync(string phoneNumber, string templateName, string language,
            IReadOnlyList<string> bodyParameters, IReadOnlyList<string> quickReplyPayloads, CancellationToken cancellationToken,
            string? bodyParameterName = null) =>
            SendTemplateAsync(phoneNumber, templateName, language, bodyParameters, quickReplyPayloads, cancellationToken, bodyParameterName, [], null);

        public Task<WhatsAppSendResult> SendTemplateAsync(string phoneNumber, string templateName, string language,
            IReadOnlyList<string> bodyParameters, IReadOnlyList<string> quickReplyPayloads, CancellationToken cancellationToken,
            string? bodyParameterName, IReadOnlyList<string> urlButtonParameters, WhatsAppDocumentHeader? documentHeader)
        {
            TemplateSends.Add((phoneNumber, documentHeader));
            return Task.FromResult(new WhatsAppSendResult(true, Guid.NewGuid().ToString("N"), null));
        }

        public Task<WhatsAppMediaUploadResult> UploadDocumentAsync(byte[] content, string fileName, string mimeType,
            CancellationToken cancellationToken)
        {
            var mediaId = $"media-{++_mediaCounter}";
            UploadCalls[mediaId] = content;
            return Task.FromResult(new WhatsAppMediaUploadResult(true, mediaId, null));
        }
    }

    private sealed class FakeConfiguration(string storageRoot) : Microsoft.Extensions.Configuration.IConfiguration
    {
        public string? this[string key]
        {
            get => key == "FileStorage:RootPath" ? storageRoot : null;
            set => throw new NotSupportedException();
        }
        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => [];
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotSupportedException();
        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => throw new NotSupportedException();
    }

    private sealed class FakeWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "CondoLink.Tests";
    }
}

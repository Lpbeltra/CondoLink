using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Api.Features.RequestMessages;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Tests;

public sealed class RequestAttachmentEndpointsTests : IAsyncLifetime
{
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "condolink-attachment-tests", Guid.NewGuid().ToString("N"));
    private CoreEndpointTestHost _host = null!;
    private Guid _authorId;
    private Guid _managerId;
    private Guid _outsiderId;
    private Guid _requestId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(
            application =>
            {
                application.MapRequestAttachments();
                application.MapCreateRequestMessage();
            },
            builder =>
            {
                builder.Configuration["FileStorage:RootPath"] = _storageRoot;
                builder.Services.AddSingleton<LocalFileStorage>();
            });

        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial Anexos", null, null);
            var otherCondominium = new Condominium("Residencial Externo", null, null);
            var author = CoreTestSeed.User("Autor", "autor-anexos@example.com");
            var manager = CoreTestSeed.User("Síndico", "sindico-anexos@example.com");
            var outsider = CoreTestSeed.User("Externo", "externo-anexos@example.com");
            var category = new Category(condominium.Id, "Manutenção", null);
            var request = new DomainRequest(
                condominium.Id, author.Id, null, category.Id,
                "Vazamento", "Vazamento na garagem");

            db.AddRange(
                condominium, otherCondominium, author, manager, outsider,
                category, request);
            CoreTestSeed.AddMember(
                db, author.Id, condominium.Id, CondominiumRole.Resident);
            CoreTestSeed.AddMember(
                db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(
                db, outsider.Id, otherCondominium.Id, CondominiumRole.Resident);
            await db.SaveChangesAsync();

            _authorId = author.Id;
            _managerId = manager.Id;
            _outsiderId = outsider.Id;
            _requestId = request.Id;
        });
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Uploads_one_image()
    {
        var response = await UploadAsync(
            _host.ClientFor(_authorId), _requestId,
            File("foto.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<RequestAttachmentEndpoints.Response[]>();
        var attachment = Assert.Single(body!);
        Assert.Equal("foto.jpg", attachment.OriginalFileName);
        Assert.Equal("image/jpeg", attachment.ContentType);
    }

    [Fact]
    public async Task Uploads_six_images_and_pdfs_in_one_request()
    {
        var files = Enumerable.Range(0, 6)
            .Select(index => File(
                $"arquivo-{index}.pdf", "application/pdf", "%PDF-test"u8.ToArray()))
            .ToArray();

        var response = await UploadAsync(
            _host.ClientFor(_authorId), _requestId, files);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<RequestAttachmentEndpoints.Response[]>();
        Assert.Equal(6, body!.Length);
    }

    [Fact]
    public async Task Rejects_more_than_six_files_without_persisting_any()
    {
        var before = await AttachmentCountAsync();
        var files = Enumerable.Range(0, 7)
            .Select(index => File(
                $"arquivo-{index}.pdf", "application/pdf", "%PDF-test"u8.ToArray()))
            .ToArray();

        var response = await UploadAsync(
            _host.ClientFor(_authorId), _requestId, files);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await AttachmentCountAsync());
        Assert.Contains(
            "no máximo 6 arquivos",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rejects_a_file_larger_than_fifteen_megabytes()
    {
        var response = await UploadAsync(
            _host.ClientFor(_authorId), _requestId,
            File(
                "grande.pdf",
                "application/pdf",
                new byte[15 * 1024 * 1024 + 1]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "no máximo 15 MB",
            await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("imagem.png", "image/png")]
    [InlineData("imagem.webp", "image/webp")]
    [InlineData("documento.pdf", "application/pdf")]
    [InlineData("video.mp4", "video/mp4")]
    public async Task Accepts_supported_file_types(string name, string contentType)
    {
        var response = await UploadAsync(
            _host.ClientFor(_authorId), _requestId,
            File(name, contentType, [1, 2, 3]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("programa.exe", "application/octet-stream")]
    [InlineData("imagem.svg", "image/svg+xml")]
    [InlineData("falso.pdf", "image/jpeg")]
    public async Task Rejects_unsupported_or_mismatched_file_types(
        string name, string contentType)
    {
        var response = await UploadAsync(
            _host.ClientFor(_authorId), _requestId,
            File(name, contentType, [1, 2, 3]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "tipo de arquivo ainda não é suportado",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Author_can_download_with_original_name_and_attachment_disposition()
    {
        var attachment = await UploadOneAsync();

        var response = await _host.ClientFor(_authorId)
            .GetAsync(attachment.ContentUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/pdf",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "attachment",
            response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("%PDF-test"u8.ToArray(), await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task User_without_request_access_cannot_download()
    {
        var attachment = await UploadOneAsync();

        var response = await _host.ClientFor(_outsiderId)
            .GetAsync(attachment.ContentUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_can_delete_and_the_attachment_disappears()
    {
        var attachment = await UploadOneAsync();

        var delete = await _host.ClientFor(_managerId)
            .DeleteAsync($"/request-attachments/{attachment.Id}");
        var download = await _host.ClientFor(_authorId)
            .GetAsync(attachment.ContentUrl);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        Assert.False(await _host.WithDbAsync(db => db.RequestAttachments
            .AnyAsync(item => item.Id == attachment.Id)));
    }

    [Fact]
    public async Task User_without_request_access_cannot_delete()
    {
        var attachment = await UploadOneAsync();

        var response = await _host.ClientFor(_outsiderId)
            .DeleteAsync($"/request-attachments/{attachment.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(await _host.WithDbAsync(db => db.RequestAttachments
            .AnyAsync(item => item.Id == attachment.Id)));
    }

    [Fact]
    public async Task Upload_to_a_missing_request_returns_not_found()
    {
        var response = await UploadAsync(
            _host.ClientFor(_authorId), Guid.NewGuid(),
            File("documento.pdf", "application/pdf", "%PDF-test"u8.ToArray()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task User_without_request_access_cannot_upload()
    {
        var response = await UploadAsync(
            _host.ClientFor(_outsiderId), _requestId,
            File("documento.pdf", "application/pdf", "%PDF-test"u8.ToArray()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Non_multipart_upload_returns_a_specific_message()
    {
        var response = await _host.ClientFor(_authorId).PostAsJsonAsync(
            $"/requests/{_requestId}/attachments",
            new { files = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "multipart/form-data",
            await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(RequestStatus.Resolved)]
    [InlineData(RequestStatus.Cancelled)]
    public async Task Closed_request_is_read_only_for_resident_but_attachment_remains_downloadable(
        RequestStatus status)
    {
        var attachment = await UploadOneAsync();
        await _host.WithDbAsync(async db =>
        {
            var request = await db.Requests.SingleAsync(item => item.Id == _requestId);
            request.ChangeStatus(status, DateTime.UtcNow);
            await db.SaveChangesAsync();
        });

        var client = _host.ClientFor(_authorId);
        var message = await client.PostAsJsonAsync(
            $"/requests/{_requestId}/messages",
            new { content = "Tentativa depois do encerramento" });
        var upload = await UploadAsync(
            client, _requestId,
            File("nova.pdf", "application/pdf", "%PDF-test"u8.ToArray()));
        var delete = await client.DeleteAsync(
            $"/request-attachments/{attachment.Id}");
        var download = await client.GetAsync(attachment.ContentUrl);

        Assert.Equal(HttpStatusCode.Conflict, message.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, upload.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Contains(
            "somente para consulta",
            await message.Content.ReadAsStringAsync());
        Assert.Contains(
            "somente para consulta",
            await upload.Content.ReadAsStringAsync());
        Assert.Contains(
            "somente para consulta",
            await delete.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.True(await _host.WithDbAsync(db => db.RequestAttachments
            .AnyAsync(item => item.Id == attachment.Id)));
    }

    private async Task<RequestAttachmentEndpoints.Response> UploadOneAsync()
    {
        var response = await UploadAsync(
            _host.ClientFor(_authorId), _requestId,
            File("documento.pdf", "application/pdf", "%PDF-test"u8.ToArray()));
        response.EnsureSuccessStatusCode();
        var body = await response.Content
            .ReadFromJsonAsync<RequestAttachmentEndpoints.Response[]>();
        return Assert.Single(body!);
    }

    private async Task<int> AttachmentCountAsync() =>
        await _host.WithDbAsync(db => db.RequestAttachments.CountAsync());

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        Guid requestId,
        params (string Name, string ContentType, byte[] Content)[] files)
    {
        using var form = new MultipartFormDataContent();
        foreach (var file in files)
        {
            var content = new ByteArrayContent(file.Content);
            content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            form.Add(content, "files", file.Name);
        }

        return await client.PostAsync(
            $"/requests/{requestId}/attachments", form);
    }

    private static (string Name, string ContentType, byte[] Content) File(
        string name, string contentType, byte[] content) =>
        (name, contentType, content);
}

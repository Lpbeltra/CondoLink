using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.CondominiumAssistant;

public static class CondominiumAssistantEndpoints
{
    public static IEndpointRouteBuilder MapCondominiumAssistant(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/condominiums/{condominiumId:guid}").RequireAuthorization();
        group.MapGet("/documents", ListDocuments);
        group.MapPost("/documents", UploadDocument).DisableAntiforgery();
        group.MapGet("/documents/{documentId:guid}/download", DownloadDocument);
        group.MapPut("/documents/{documentId:guid}/active", SetDocumentActive);
        group.MapDelete("/documents/{documentId:guid}", DeleteDocument);
        group.MapPost("/documents/{documentId:guid}/reprocess", ReprocessDocument);
        group.MapPut("/documents/bulk/active", SetDocumentsActive);
        group.MapPost("/documents/bulk/delete", DeleteDocuments);
        group.MapPost("/assistant/conversations", CreateConversation);
        group.MapPost("/assistant/messages", StartConversation);
        group.MapGet("/assistant/conversations", ListConversations);
        group.MapGet("/assistant/conversations/{conversationId:guid}", GetConversation);
        group.MapPost("/assistant/conversations/{conversationId:guid}/messages", Ask);
        group.MapDelete("/assistant/conversations/{conversationId:guid}/request-context", RemoveContext);
        group.MapDelete("/assistant/conversations/{conversationId:guid}", DeleteConversation);
        return app;
    }

    private static async Task<IResult> ListDocuments(Guid condominiumId, ClaimsPrincipal principal,
        AppDbContext db, [Microsoft.AspNetCore.Mvc.FromServices] IEmbeddingService embeddings,
        CancellationToken ct)
    {
        var access = await Access(condominiumId, principal, db, ct); if (access.Error is not null) return access.Error;
        return Results.Ok(await db.CondominiumDocuments.AsNoTracking().Where(x => x.CondominiumId == condominiumId)
            .OrderByDescending(x => x.UpdatedAt).Select(x => new { x.Id, x.Name, x.DocumentType,
                x.OriginalFileName, x.Version, x.DocumentDate, x.IsActive, x.ProcessingStatus,
                x.ProcessingError, x.CreatedAt, x.UpdatedAt,
                NeedsReindexing = x.ProcessingStatus == CondominiumDocumentProcessingStatus.Ready
                    && !db.CondominiumDocumentChunks.Any(chunk => chunk.CondominiumDocumentId == x.Id
                        && chunk.EmbeddingModel == embeddings.Model),
                NeedsKnowledgeUpdate = x.ProcessingStatus == CondominiumDocumentProcessingStatus.Ready
                    && !db.CondominiumDocumentKnowledge.Any(item => item.CondominiumDocumentId == x.Id) }).ToArrayAsync(ct));
    }

    private static async Task<IResult> UploadDocument(Guid condominiumId, HttpRequest request,
        ClaimsPrincipal principal, AppDbContext db, LocalFileStorage storage,
        CondominiumDocumentProcessor processor, IOptions<CondominiumAssistantOptions> options,
        CancellationToken ct)
    {
        var access = await Access(condominiumId, principal, db, ct); if (access.Error is not null) return access.Error;
        if (!request.HasFormContentType) return UploadBadRequest(
            "DocumentMultipartRequired", "Envie o documento como multipart/form-data.");
        var form = await request.ReadFormAsync(ct); var file = form.Files.GetFile("file");
        var fileError = ValidateDocumentFile(file, options.Value.MaximumFileBytes);
        if (fileError is not null) return UploadBadRequest(fileError.Code, fileError.Message);
        var uploadedFile = file!;
        var extension = Path.GetExtension(uploadedFile.FileName).ToLowerInvariant();
        if (!Enum.TryParse<CondominiumDocumentType>(form["documentType"].FirstOrDefault(), true, out var type)) type = CondominiumDocumentType.Other;
        var name = form["name"].FirstOrDefault()?.Trim(); if (string.IsNullOrWhiteSpace(name) || name.Length > 200) return UploadBadRequest(
            "DocumentNameInvalid", "Informe um nome de até 200 caracteres.");
        var version = int.TryParse(form["version"].FirstOrDefault(), out var parsedVersion) && parsedVersion > 0 ? parsedVersion : 1;
        DateOnly? date = DateOnly.TryParse(form["documentDate"].FirstOrDefault(), out var parsedDate) ? parsedDate : null;
        var document = new CondominiumDocument(condominiumId, name, type, Path.GetFileName(uploadedFile.FileName),
            string.Empty, uploadedFile.ContentType ?? "application/octet-stream", version, date, access.UserId);
        await using var input = uploadedFile.OpenReadStream();
        var key = await storage.SaveCondominiumDocumentAsync(condominiumId, document.Id, input, extension, ct);
        document.SetStorageKey(key); db.CondominiumDocuments.Add(document); await db.SaveChangesAsync(ct);
        await using var saved = storage.OpenRead(key)!; await processor.ProcessAsync(document, saved, extension, ct);
        return Results.Created($"/condominiums/{condominiumId}/documents/{document.Id}", new { document.Id, document.ProcessingStatus, document.ProcessingError });
    }

    private static IResult UploadBadRequest(string code, string message) =>
        Results.BadRequest(new { code, message });

    internal static DocumentUploadValidationError? ValidateDocumentFile(IFormFile? file, long maximumFileBytes)
    {
        if (file is null || file.Length == 0)
            return new("DocumentFileRequired", "Selecione um arquivo válido.");
        if (file.Length > maximumFileBytes)
            return new("DocumentFileTooLarge",
                $"O arquivo excede o limite de {CondominiumAssistantOptions.MaximumFileSizeMegabytes} MB.");
        if (Path.GetExtension(file.FileName).ToLowerInvariant() is not (".pdf" or ".docx" or ".txt"))
            return new("DocumentFileTypeUnsupported", "Formato não suportado. Envie um arquivo PDF, DOCX ou TXT.");
        return null;
    }

    internal sealed record DocumentUploadValidationError(string Code, string Message);

    private static async Task<IResult> DownloadDocument(Guid condominiumId, Guid documentId,
        ClaimsPrincipal principal, AppDbContext db, LocalFileStorage storage, CancellationToken ct)
    {
        var access = await Access(condominiumId, principal, db, ct); if (access.Error is not null) return access.Error;
        var document = await db.CondominiumDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == documentId && x.CondominiumId == condominiumId, ct);
        if (document is null) return Results.NotFound(); var stream = storage.OpenRead(document.StorageKey);
        return stream is null ? Results.NotFound() : Results.File(stream, document.MimeType, document.OriginalFileName);
    }

    private static async Task<IResult> SetDocumentActive(Guid condominiumId, Guid documentId,
        ActiveRequest body, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var access = await Access(condominiumId, principal, db, ct); if (access.Error is not null) return access.Error;
        var document = await db.CondominiumDocuments.SingleOrDefaultAsync(x => x.Id == documentId && x.CondominiumId == condominiumId, ct);
        if (document is null) return Results.NotFound();
        if (body.Active && document.ProcessingStatus != CondominiumDocumentProcessingStatus.Ready)
            return Results.Conflict(new { code = "DocumentNotReady", message = "Somente documentos prontos podem ser ativados." });
        document.SetActive(body.Active); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> SetDocumentsActive(Guid condominiumId, BulkActiveRequest body,
        ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var access = await Access(condominiumId, principal, db, ct); if (access.Error is not null) return access.Error;
        var ids = body.DocumentIds.Distinct().Take(200).ToArray();
        var documents = await db.CondominiumDocuments.Where(x => x.CondominiumId == condominiumId && ids.Contains(x.Id)).ToArrayAsync(ct);
        var failed = new List<object>(); var updated = 0;
        foreach (var document in documents)
        {
            if (body.Active && document.ProcessingStatus != CondominiumDocumentProcessingStatus.Ready)
            { failed.Add(new { documentId = document.Id, reason = "Somente documentos prontos podem ser ativados." }); continue; }
            document.SetActive(body.Active); updated++;
        }
        foreach (var missing in ids.Except(documents.Select(x => x.Id))) failed.Add(new { documentId = missing, reason = "Documento não encontrado." });
        await db.SaveChangesAsync(ct); return Results.Ok(new { succeeded = updated, failed });
    }

    private static async Task<IResult> DeleteDocuments(Guid condominiumId, BulkDocumentRequest body,
        ClaimsPrincipal principal, AppDbContext db, ICondominiumDocumentStorage storage,
        ILogger<CondominiumDocumentProcessor> logger, CancellationToken ct)
    {
        var access = await Access(condominiumId, principal, db, ct); if (access.Error is not null) return access.Error;
        var ids = body.DocumentIds.Distinct().Take(200).ToArray(); var succeeded = 0; var failed = new List<object>();
        foreach (var id in ids)
        {
            var document = await db.CondominiumDocuments.SingleOrDefaultAsync(x => x.Id == id && x.CondominiumId == condominiumId, ct);
            if (document is null) { failed.Add(new { documentId = id, reason = "Documento não encontrado." }); continue; }
            if (document.ProcessingStatus == CondominiumDocumentProcessingStatus.Processing)
            { failed.Add(new { documentId = id, reason = "O documento ainda está em processamento." }); continue; }
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                db.CondominiumDocuments.Remove(document); await db.SaveChangesAsync(ct);
                storage.DeleteCondominiumDocument(condominiumId, id, document.StorageKey);
                await transaction.CommitAsync(ct); succeeded++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear(); failed.Add(new { documentId = id, reason = "Não foi possível excluir o documento." });
                logger.LogWarning("Bulk document deletion failed. CondominiumId: {CondominiumId}; DocumentId: {DocumentId}; FailureType: {FailureType}.", condominiumId, id, exception.GetType().Name);
            }
        }
        return Results.Ok(new { succeeded, failed });
    }

    internal static async Task<IResult> DeleteDocument(Guid condominiumId, Guid documentId,
        ClaimsPrincipal principal, AppDbContext db,
        [Microsoft.AspNetCore.Mvc.FromServices] ICondominiumDocumentStorage storage,
        ILogger<CondominiumDocumentProcessor> logger, CancellationToken ct)
    {
        var access = await Access(condominiumId, principal, db, ct); if (access.Error is not null) return access.Error;
        var document = await db.CondominiumDocuments.SingleOrDefaultAsync(
            x => x.Id == documentId && x.CondominiumId == condominiumId, ct);
        if (document is null) return Results.NotFound();
        if (document.ProcessingStatus == CondominiumDocumentProcessingStatus.Processing)
            return Results.Conflict(new { code = "DocumentProcessing", message = "Aguarde o processamento terminar antes de excluir o documento." });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.CondominiumDocuments.Remove(document);
            await db.SaveChangesAsync(ct);
            storage.DeleteCondominiumDocument(condominiumId, documentId, document.StorageKey);
            await transaction.CommitAsync(ct);
            return Results.NoContent();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            logger.LogWarning("Document deletion failed. CondominiumId: {CondominiumId}; DocumentId: {DocumentId}; FailureType: {FailureType}.",
                condominiumId, documentId, exception.GetType().Name);
            return Results.Json(new { code = "DocumentDeleteFailed", message = "Não foi possível excluir o documento. Tente novamente." }, statusCode: 503);
        }
    }

    private static async Task<IResult> ReprocessDocument(Guid condominiumId, Guid documentId,
        ClaimsPrincipal principal, AppDbContext db, LocalFileStorage storage,
        CondominiumDocumentProcessor processor, CancellationToken ct)
    {
        var access = await Access(condominiumId, principal, db, ct); if (access.Error is not null) return access.Error;
        var document = await db.CondominiumDocuments.SingleOrDefaultAsync(
            x => x.Id == documentId && x.CondominiumId == condominiumId, ct);
        if (document is null) return Results.NotFound();
        if (document.ProcessingStatus == CondominiumDocumentProcessingStatus.Processing)
            return Results.Conflict(new { code = "DocumentProcessing", message = "O documento já está sendo processado." });
        await using var stream = storage.OpenRead(document.StorageKey);
        if (stream is null) return Results.NotFound(new { code = "DocumentFileMissing", message = "O arquivo do documento não foi encontrado." });
        await processor.ProcessAsync(document, stream, Path.GetExtension(document.OriginalFileName), ct);
        return Results.Ok(new { document.Id, document.ProcessingStatus, document.ProcessingError });
    }

    private static async Task<IResult> CreateConversation(Guid condominiumId, CreateConversationRequest body,
        ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var access = await Access(condominiumId, principal, db, ct); if (access.Error is not null) return access.Error;
        if (body.RequestId is Guid requestId && !await db.Requests.AnyAsync(x => x.Id == requestId && x.CondominiumId == condominiumId, ct)) return Results.NotFound(new { error = "Solicitação não encontrada neste condomínio." });
        var conversation = new CondominiumAssistantConversation(condominiumId, access.UserId, body.RequestId,
            string.IsNullOrWhiteSpace(body.Title) ? "Nova conversa" : body.Title[..Math.Min(body.Title.Length, 200)]);
        db.CondominiumAssistantConversations.Add(conversation); await db.SaveChangesAsync(ct); return Results.Ok(conversation);
    }

    private static async Task<IResult> ListConversations(Guid condominiumId, ClaimsPrincipal principal,
        AppDbContext db, CancellationToken ct, int page = 1, int pageSize = 20, string? search = null)
    {
        var access = await Access(condominiumId, principal, db, ct); if (access.Error is not null) return access.Error;
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.CondominiumAssistantConversations.AsNoTracking()
            .Where(x => x.CondominiumId == condominiumId && x.CreatedByUserId == access.UserId);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Title.ToLower().Contains(search.Trim().ToLower()));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.Title, x.RequestId, x.CreatedAt, x.UpdatedAt,
                RequestTitle = x.RequestId == null ? null : db.Requests
                    .Where(r => r.Id == x.RequestId && r.CondominiumId == condominiumId)
                    .Select(r => r.Title).FirstOrDefault() })
            .ToArrayAsync(ct);
        return Results.Ok(new { items, page, pageSize, total, hasMore = page * pageSize < total });
    }

    private static async Task<IResult> GetConversation(Guid condominiumId, Guid conversationId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var access = await Access(condominiumId, principal, db, ct); if (access.Error is not null) return access.Error;
        var conversation = await OwnConversation(condominiumId, conversationId, access.UserId, db, ct); if (conversation is null) return Results.NotFound();
        var messageRows = await db.CondominiumAssistantMessages.AsNoTracking().Where(x => x.ConversationId == conversationId).OrderBy(x => x.CreatedAt)
            .Select(x => new { x.Id, x.Role, x.Content, x.SourcesJson, x.CreatedAt }).ToArrayAsync(ct);
        var sourceIds = messageRows.SelectMany(x => ParseSources(x.SourcesJson)).Select(x => x.DocumentId).Distinct().ToArray();
        var availableDocuments = await db.CondominiumDocuments.AsNoTracking()
            .Where(x => x.CondominiumId == condominiumId && sourceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.IsActive, ct);
        var messages = messageRows.Select(x => new { x.Id, Role = x.Role.ToString(), x.Content, x.CreatedAt,
            Sources = ParseSources(x.SourcesJson).Select(source => new
            { Source = source, DocumentExists = availableDocuments.ContainsKey(source.DocumentId),
                DocumentCurrentlyActive = availableDocuments.GetValueOrDefault(source.DocumentId) }).ToArray() }).ToArray();
        object? requestContext = null; var contextUnavailable = false;
        if (conversation.RequestId is Guid requestId)
        {
            requestContext = await db.Requests.AsNoTracking().Where(x => x.Id == requestId && x.CondominiumId == condominiumId)
                .Select(x => new { x.Id, x.Title }).SingleOrDefaultAsync(ct);
            contextUnavailable = requestContext is null;
        }
        return Results.Ok(new { conversation, messages, requestContext, contextUnavailable });
    }

    private static async Task<IResult> Ask(Guid condominiumId, Guid conversationId, AskRequest body,
        bool? stream, ClaimsPrincipal principal, AppDbContext db, CondominiumAssistantService assistant,
        IOptions<CondominiumAssistantOptions> options, HttpResponse response, CancellationToken ct)
    {
        var access = await Access(condominiumId, principal, db, ct); if (access.Error is not null) return access.Error;
        var conversation = await OwnConversation(condominiumId, conversationId, access.UserId, db, ct); if (conversation is null) return Results.NotFound();
        var question = body.Question?.Trim(); if (string.IsNullOrWhiteSpace(question) || question.Length > options.Value.MaximumQuestionCharacters) return Results.BadRequest(new { error = "Informe uma pergunta de até 2.000 caracteres." });
        if (conversation.RequestId is Guid requestId && !await db.Requests.AnyAsync(x => x.Id == requestId && x.CondominiumId == condominiumId, ct))
        { conversation.RemoveRequestContext(); await db.SaveChangesAsync(ct); }

        if (stream == true && options.Value.StreamingEnabled)
        {
            db.CondominiumAssistantMessages.Add(new(conversationId, CondominiumAssistantRole.User, question));
            conversation.Touch();
            await db.SaveChangesAsync(ct);
            return await StreamAnswerAsync(response, db, assistant, conversation, question, isNewConversation: false, ct);
        }

        try
        {
            db.CondominiumAssistantMessages.Add(new(conversationId, CondominiumAssistantRole.User, question));
            conversation.Touch();
            await db.SaveChangesAsync(ct);
            var result = await assistant.AskAsync(conversation, question, ct);
            db.CondominiumAssistantMessages.Add(new(conversationId, CondominiumAssistantRole.Assistant, result.Answer, JsonSerializer.Serialize(result.Sources)));
            await db.SaveChangesAsync(ct); return Results.Ok(result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return Results.Json(new { error = "O assistente está temporariamente indisponível. Tente novamente." }, statusCode: 503); }
    }

    private static async Task<IResult> StartConversation(Guid condominiumId, StartConversationRequest body,
        bool? stream, ClaimsPrincipal principal, AppDbContext db, CondominiumAssistantService assistant,
        IOptions<CondominiumAssistantOptions> options, HttpResponse response, CancellationToken ct)
    {
        var access = await Access(condominiumId, principal, db, ct); if (access.Error is not null) return access.Error;
        var question = body.Question?.Trim();
        if (string.IsNullOrWhiteSpace(question) || question.Length > options.Value.MaximumQuestionCharacters)
            return Results.BadRequest(new { error = "Informe uma pergunta de até 2.000 caracteres." });
        if (body.RequestId is Guid requestId && !await db.Requests.AnyAsync(x => x.Id == requestId && x.CondominiumId == condominiumId, ct))
            body = body with { RequestId = null };
        var conversation = new CondominiumAssistantConversation(
            condominiumId, access.UserId, body.RequestId, "Nova conversa");
        conversation.SetInitialTitle(AutomaticTitle(question));
        db.CondominiumAssistantConversations.Add(conversation);
        db.CondominiumAssistantMessages.Add(new(conversation.Id, CondominiumAssistantRole.User, question));
        await db.SaveChangesAsync(ct);

        if (stream == true && options.Value.StreamingEnabled)
            return await StreamAnswerAsync(response, db, assistant, conversation, question, isNewConversation: true, ct);

        try
        {
            var result = await assistant.AskAsync(conversation, question, ct);
            db.CondominiumAssistantMessages.Add(new(conversation.Id,
                CondominiumAssistantRole.Assistant, result.Answer,
                JsonSerializer.Serialize(result.Sources)));
            conversation.Touch(); await db.SaveChangesAsync(ct);
            return Results.Ok(new { conversation, result.Answer, result.Sources });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Results.Json(new { error = "A conversa foi salva, mas o assistente está temporariamente indisponível.", conversationId = conversation.Id }, statusCode: 503);
        }
    }

    /// <summary>
    /// Writes the assistant's answer as Server-Sent Events instead of a single
    /// JSON payload: a <c>sources</c> event once retrieval finishes (all recovered
    /// sources, informational only), one <c>token</c> event per streamed delta, and
    /// a final <c>done</c> event carrying the full answer and citation-filtered
    /// sources — the same shape the non-streaming response returns, so the
    /// frontend only needs to read the final event to reconcile state. On failure,
    /// an <c>error</c> event is sent instead; a client-initiated cancellation
    /// (<paramref name="ct"/> firing) is left to propagate and drops the
    /// connection without writing anything further.
    /// </summary>
    private static async Task<IResult> StreamAnswerAsync(HttpResponse response, AppDbContext db,
        CondominiumAssistantService assistant, CondominiumAssistantConversation conversation,
        string question, bool isNewConversation, CancellationToken ct)
    {
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";

        async Task WriteEventAsync(string eventName, object payload)
        {
            await response.WriteAsync($"event: {eventName}\ndata: {JsonSerializer.Serialize(payload)}\n\n", ct);
            await response.Body.FlushAsync(ct);
        }

        try
        {
            var result = await assistant.AskStreamAsync(conversation, question,
                (sources, token) => WriteEventAsync("sources", new { sources }),
                (delta, token) => WriteEventAsync("token", new { delta }),
                ct);

            db.CondominiumAssistantMessages.Add(new(conversation.Id, CondominiumAssistantRole.Assistant,
                result.Answer, JsonSerializer.Serialize(result.Sources)));
            conversation.Touch();
            await db.SaveChangesAsync(ct);

            await WriteEventAsync("done", new
            {
                conversation,
                answer = result.Answer,
                sources = result.Sources,
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await WriteEventAsync("error", new
            {
                message = isNewConversation
                    ? "A conversa foi salva, mas o assistente está temporariamente indisponível."
                    : "O assistente está temporariamente indisponível. Tente novamente.",
            });
        }

        return Results.Empty;
    }

    private static async Task<IResult> DeleteConversation(Guid condominiumId, Guid conversationId,
        ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var access = await Access(condominiumId, principal, db, ct); if (access.Error is not null) return access.Error;
        var conversation = await OwnConversation(condominiumId, conversationId, access.UserId, db, ct);
        if (conversation is null) return Results.NotFound();
        db.CondominiumAssistantConversations.Remove(conversation);
        await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> RemoveContext(Guid condominiumId, Guid conversationId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var access = await Access(condominiumId, principal, db, ct); if (access.Error is not null) return access.Error;
        var conversation = await OwnConversation(condominiumId, conversationId, access.UserId, db, ct); if (conversation is null) return Results.NotFound();
        conversation.RemoveRequestContext(); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static Task<CondominiumAssistantConversation?> OwnConversation(Guid condominiumId, Guid id, Guid userId, AppDbContext db, CancellationToken ct) =>
        db.CondominiumAssistantConversations.SingleOrDefaultAsync(x => x.Id == id && x.CondominiumId == condominiumId && x.CreatedByUserId == userId, ct);

    private static async Task<(Guid UserId, IResult? Error)> Access(Guid condominiumId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var claim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var userId)) return (Guid.Empty, Results.Unauthorized());
        if (principal.IsInRole(DependencyInjection.PlatformAdminRole)) return (userId, null);
        var manager = await db.CondominiumMemberships.AsNoTracking().Where(x => x.UserId == userId && x.CondominiumId == condominiumId && x.IsActive && x.EndedAt == null)
            .Join(db.CondominiumMembershipRoles.AsNoTracking().Where(x => (x.Role == CondominiumRole.Manager || x.Role == CondominiumRole.SubManager) && x.IsActive && x.RevokedAt == null), x => x.Id, x => x.CondominiumMembershipId, (_, _) => true).AnyAsync(ct);
        return manager ? (userId, null) : (userId, Results.Forbid());
    }

    public sealed record ActiveRequest(bool Active);
    public sealed record BulkActiveRequest(Guid[] DocumentIds, bool Active);
    public sealed record BulkDocumentRequest(Guid[] DocumentIds);
    public sealed record CreateConversationRequest(Guid? RequestId, string? Title);
    public sealed record AskRequest(string? Question);
    public sealed record StartConversationRequest(string? Question, Guid? RequestId);

    private static AssistantSource[] ParseSources(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<AssistantSource[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    internal static string AutomaticTitle(string question)
    {
        string[] ignored = ["qual", "quais", "como", "que", "é", "são", "o", "a", "os", "as", "do", "da", "dos", "das", "para", "sobre", "permitido", "permitida"];
        var words = System.Text.RegularExpressions.Regex.Matches(question.Trim(), @"[\p{L}\p{N}]+")
            .Select(x => x.Value).Where(x => !ignored.Contains(x, StringComparer.OrdinalIgnoreCase)).Take(6).ToArray();
        var title = words.Length == 0 ? question.Trim() : string.Join(' ', words);
        title = char.ToUpperInvariant(title[0]) + title[1..];
        return title[..Math.Min(title.Length, 60)];
    }
}

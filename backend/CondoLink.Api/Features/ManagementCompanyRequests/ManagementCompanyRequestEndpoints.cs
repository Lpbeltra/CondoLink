using System.Security.Claims;
using System.Text.Json;
using CondoLink.Api.Common;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Api.Features.ManagementCompanyRequests;

public static class ManagementCompanyRequestEndpoints
{
    public static IEndpointRouteBuilder MapManagementCompanyRequests(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/management-company-requests").RequireAuthorization();
        group.MapGet("", List);
        group.MapGet("/options", Options);
        group.MapPost("/fines", CreateFine);
        group.MapPost("/payments", CreatePayment);
        group.MapPost("/questions", CreateQuestion);
        group.MapPost("/fines/multipart", CreateFineMultipart).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(AttachmentPolicy.MaximumRequestSize));
        group.MapPost("/payments/multipart", CreatePaymentMultipart).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(AttachmentPolicy.MaximumRequestSize));
        group.MapPost("/questions/multipart", CreateQuestionMultipart).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(AttachmentPolicy.MaximumRequestSize));
        group.MapGet("/{id:guid}", GetDetail);
        group.MapPost("/{id:guid}/start-processing", StartProcessing);
        group.MapPost("/{id:guid}/messages", AddMessage);
        group.MapPost("/{id:guid}/interactions", Interact).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(AttachmentPolicy.MaximumRequestSize));
        group.MapPost("/{id:guid}/status", ChangeStatus);
        group.MapPost("/{id:guid}/complete-payment", CompletePayment).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(AttachmentPolicy.MaximumRequestSize));
        group.MapPost("/{id:guid}/cancel", Cancel);
        group.MapPost("/{id:guid}/attachments", UploadAttachments).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(AttachmentPolicy.MaximumRequestSize));
        group.MapPut("/{id:guid}/multipart", UpdateMultipart).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(AttachmentPolicy.MaximumRequestSize));
        group.MapGet("/{id:guid}/attachments", ListAttachments);
        app.MapGet("/management-company-request-attachments/{attachmentId:guid}/content", DownloadAttachment).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> List(ClaimsPrincipal user, AppDbContext db, ManagementCompanyRequestAccessService access, CancellationToken ct, Guid? condominiumId = null, ManagementCompanyRequestType? type = null, ManagementCompanyRequestStatus? status = null, string? search = null, DateOnly? from = null, DateOnly? to = null, bool includeCompleted = false, bool includeCancelled = false, int page = 1, int pageSize = 20)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 50);
        var userId = await access.RequireUserIdAsync(user, ct);
        var scoped = db.CondominiumMemberships.AsNoTracking().Where(m => m.UserId == userId && m.IsActive && m.EndedAt == null).Join(db.CondominiumMembershipRoles.AsNoTracking().Where(r => r.IsActive && r.RevokedAt == null && (r.Role == CondominiumRole.Manager || r.Role == CondominiumRole.SubManager)), m => m.Id, r => r.CondominiumMembershipId, (m, r) => m.CondominiumId).Distinct();
        if (condominiumId.HasValue && !await scoped.ContainsAsync(condominiumId.Value, ct)) throw new ForbiddenAppException("Você não possui acesso de gestão a este condomínio.");
        if (from.HasValue && to.HasValue && from > to) throw new ValidationAppException("A data inicial não pode ser posterior à data final.");
        var query = db.ManagementCompanyRequests.AsNoTracking().Where(r => scoped.Contains(r.CondominiumId));
        if (condominiumId.HasValue) query = query.Where(r => r.CondominiumId == condominiumId);
        if (type.HasValue) query = query.Where(r => r.Type == type);
        if (status.HasValue) query = query.Where(r => r.Status == status);
        else if (!includeCompleted && !includeCancelled) query = query.Where(r => r.Status != ManagementCompanyRequestStatus.Completed && r.Status != ManagementCompanyRequestStatus.Cancelled);
        else if (includeCompleted && !includeCancelled) query = query.Where(r => r.Status == ManagementCompanyRequestStatus.Completed || r.Status != ManagementCompanyRequestStatus.Cancelled);
        else if (!includeCompleted && includeCancelled) query = query.Where(r => r.Status != ManagementCompanyRequestStatus.Completed);
        if (from.HasValue) { var start = DateTime.SpecifyKind(from.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc); query = query.Where(r => r.CreatedAt >= start); }
        if (to.HasValue) { var end = DateTime.SpecifyKind(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc); query = query.Where(r => r.CreatedAt < end); }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(r => r.FriendlyIdentifier.Contains(q) || db.ManagementCompanyFineRequests.Any(x => x.RequestId == r.Id && x.Nature.Contains(q)) || db.ManagementCompanyPaymentRequests.Any(x => x.RequestId == r.Id && x.Nature.Contains(q)) || db.ManagementCompanyGeneralQuestionRequests.Any(x => x.RequestId == r.Id && x.Theme.Contains(q)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(r => new
        {
            r.Id, r.FriendlyIdentifier, r.CondominiumId, CondominiumName = db.Condominiums.Where(c => c.Id == r.CondominiumId).Select(c => c.Name).First(),
            r.ManagementCompanyId, ManagementCompanyName = db.ManagementCompanies.Where(c => c.Id == r.ManagementCompanyId).Select(c => c.Name).First(),
            r.Type, r.Status,
            Subject = r.Type == ManagementCompanyRequestType.Fine ? db.ManagementCompanyFineRequests.Where(x => x.RequestId == r.Id).Select(x => x.Nature).First() : r.Type == ManagementCompanyRequestType.Payment ? db.ManagementCompanyPaymentRequests.Where(x => x.RequestId == r.Id).Select(x => x.Nature).First() : db.ManagementCompanyGeneralQuestionRequests.Where(x => x.RequestId == r.Id).Select(x => x.Theme).First(),
            r.CreatedAt, r.UpdatedAt
        }).ToListAsync(ct);
        return Results.Ok(new { items, page, pageSize, total, hasMore = page * pageSize < total });
    }

    private static async Task<IResult> Options(Guid condominiumId, ClaimsPrincipal user, AppDbContext db, ManagementCompanyRequestAccessService access, CancellationToken ct)
    {
        await access.RequireManagementAsync(user, condominiumId, ct);
        var link = await db.CondominiumManagementCompanyLinks.AsNoTracking().SingleOrDefaultAsync(x => x.CondominiumId == condominiumId && x.IsActive, ct);
        if (link is null) return Results.Ok(new { condominiumId, managementCompany = (object?)null, categories = Array.Empty<object>(), units = Array.Empty<object>(), beneficiaries = Array.Empty<object>() });
        var categories = await db.ManagementCompanyRequestCategories.AsNoTracking().Where(c => c.ManagementCompanyId == link.ManagementCompanyId && c.IsActive && db.ManagementCompanyRequestCategoryResponsibles.Any(r => r.ManagementCompanyRequestCategoryId == c.Id && db.ManagementCompanyEmployees.Any(e => e.Id == r.ManagementCompanyEmployeeId && e.IsActive && db.Users.Any(u => u.Id == e.UserId && u.IsActive)))).Select(c => new { c.Id, c.Name, Type = c.FormType == ManagementCompanyRequestFormType.UnitFine ? ManagementCompanyRequestType.Fine : c.FormType == ManagementCompanyRequestFormType.SupplierPayment ? ManagementCompanyRequestType.Payment : ManagementCompanyRequestType.GeneralQuestion }).ToListAsync(ct);
        var units = await db.Units.AsNoTracking().Where(u => u.CondominiumId == condominiumId && u.IsActive).OrderBy(u => u.Identifier).Select(u => new { u.Id, u.Identifier, u.BlockId, Block = u.BlockId == null ? null : db.CondominiumBlocks.Where(b => b.Id == u.BlockId).Select(b => b.Identifier).FirstOrDefault() }).ToListAsync(ct);
        var beneficiaries = await db.CondominiumMemberships.AsNoTracking().Where(m => m.CondominiumId == condominiumId && m.IsActive && m.EndedAt == null).Join(db.CondominiumMembershipRoles.AsNoTracking().Where(r => r.IsActive && r.RevokedAt == null && (r.Role == CondominiumRole.Manager || r.Role == CondominiumRole.SubManager)), m => m.Id, r => r.CondominiumMembershipId, (m, r) => new { m.UserId, r.Role }).Join(db.Users.AsNoTracking().Where(u => u.IsActive), x => x.UserId, u => u.Id, (x, u) => new { u.Id, u.FullName, Role = x.Role, u.PixKeyType, u.PixKey }).Distinct().ToListAsync(ct);
        var company = await db.ManagementCompanies.AsNoTracking().Where(x => x.Id == link.ManagementCompanyId).Select(x => new { x.Id, x.Name }).SingleAsync(ct);
        return Results.Ok(new { condominiumId, managementCompany = company, categories, units, beneficiaries });
    }

    private static async Task<IResult> CreateFine(CreateFineCommand body, ClaimsPrincipal user, HttpContext http, ManagementCompanyRequestService service, ManagementCompanyRequestNotificationService notifications, ILogger<ManagementCompanyRequestNotificationService> logger, CancellationToken ct)
    { var request = await service.CreateFineAsync(user, body, ct); await NotifySafeAsync(() => notifications.NotifyCreatedAsync(request, ct), logger, request.Id, "Created"); await BroadcastSafeAsync(() => http.RequestServices.GetRequiredService<ManagementCompanyRequestRealtimeService>().BroadcastUpdatedAsync(request, ct), logger, request.Id, "RealtimeUpdated"); return Results.Created($"/management-company-requests/{request.Id}", null); }
    private static async Task<IResult> CreatePayment(CreatePaymentCommand body, ClaimsPrincipal user, HttpContext http, ManagementCompanyRequestService service, ManagementCompanyRequestNotificationService notifications, ILogger<ManagementCompanyRequestNotificationService> logger, CancellationToken ct)
    { var request = await service.CreatePaymentAsync(user, body, ct); await NotifySafeAsync(() => notifications.NotifyCreatedAsync(request, ct), logger, request.Id, "Created"); await BroadcastSafeAsync(() => http.RequestServices.GetRequiredService<ManagementCompanyRequestRealtimeService>().BroadcastUpdatedAsync(request, ct), logger, request.Id, "RealtimeUpdated"); return Results.Created($"/management-company-requests/{request.Id}", null); }
    private static async Task<IResult> CreateQuestion(CreateQuestionCommand body, ClaimsPrincipal user, HttpContext http, ManagementCompanyRequestService service, ManagementCompanyRequestNotificationService notifications, ILogger<ManagementCompanyRequestNotificationService> logger, CancellationToken ct)
    { var request = await service.CreateQuestionAsync(user, body, ct); await NotifySafeAsync(() => notifications.NotifyCreatedAsync(request, ct), logger, request.Id, "Created"); await BroadcastSafeAsync(() => http.RequestServices.GetRequiredService<ManagementCompanyRequestRealtimeService>().BroadcastUpdatedAsync(request, ct), logger, request.Id, "RealtimeUpdated"); return Results.Created($"/management-company-requests/{request.Id}", null); }
    private static async Task<IResult> CreateFineMultipart(HttpRequest h, ClaimsPrincipal u, ManagementCompanyRequestService s, ManagementCompanyRequestNotificationService notifications, HttpContext http, ILogger<ManagementCompanyRequestNotificationService> logger, CancellationToken ct) { var (b, f, _) = await Multipart<CreateFineCommand>(h, ct); var r = await s.CreateFineAsync(u, b, ct, f); await NotifySafeAsync(() => notifications.NotifyCreatedAsync(r, ct), logger, r.Id, "Created"); await BroadcastSafeAsync(() => http.RequestServices.GetRequiredService<ManagementCompanyRequestRealtimeService>().BroadcastUpdatedAsync(r, ct), logger, r.Id, "RealtimeUpdated"); return Results.Created($"/management-company-requests/{r.Id}", new { r.Id, r.FriendlyIdentifier }); }
    private static async Task<IResult> CreatePaymentMultipart(HttpRequest h, ClaimsPrincipal u, ManagementCompanyRequestService s, ManagementCompanyRequestNotificationService notifications, HttpContext http, ILogger<ManagementCompanyRequestNotificationService> logger, CancellationToken ct) { var (b, f, boleto) = await Multipart<CreatePaymentCommand>(h, ct); var r = await s.CreatePaymentAsync(u, b, ct, f, boleto); await NotifySafeAsync(() => notifications.NotifyCreatedAsync(r, ct), logger, r.Id, "Created"); await BroadcastSafeAsync(() => http.RequestServices.GetRequiredService<ManagementCompanyRequestRealtimeService>().BroadcastUpdatedAsync(r, ct), logger, r.Id, "RealtimeUpdated"); return Results.Created($"/management-company-requests/{r.Id}", new { r.Id, r.FriendlyIdentifier }); }
    private static async Task<IResult> CreateQuestionMultipart(HttpRequest h, ClaimsPrincipal u, ManagementCompanyRequestService s, ManagementCompanyRequestNotificationService notifications, HttpContext http, ILogger<ManagementCompanyRequestNotificationService> logger, CancellationToken ct) { var (b, f, _) = await Multipart<CreateQuestionCommand>(h, ct); var r = await s.CreateQuestionAsync(u, b, ct, f); await NotifySafeAsync(() => notifications.NotifyCreatedAsync(r, ct), logger, r.Id, "Created"); await BroadcastSafeAsync(() => http.RequestServices.GetRequiredService<ManagementCompanyRequestRealtimeService>().BroadcastUpdatedAsync(r, ct), logger, r.Id, "RealtimeUpdated"); return Results.Created($"/management-company-requests/{r.Id}", new { r.Id, r.FriendlyIdentifier }); }

    private static async Task<IResult> GetDetail(Guid id, ClaimsPrincipal user, AppDbContext db, ManagementCompanyRequestAccessService access, CancellationToken ct)
    {
        await access.RequireForRequestAsync(user, id, ct);
        return Results.Ok(await ToDetail(id, db, ct));
    }

    private static async Task<IResult> StartProcessing(Guid id, ClaimsPrincipal user, HttpContext http, AppDbContext db, ManagementCompanyRequestAccessService access, ManagementCompanyRequestService service, CancellationToken ct)
    { var actor = await access.RequireForRequestAsync(user, id, ct); var request = await Tracked(id, db, ct); await service.StartProcessingAsync(request, actor, ct); await BroadcastSafeAsync(() => http.RequestServices.GetRequiredService<ManagementCompanyRequestRealtimeService>().BroadcastUpdatedAsync(request, ct), null, request.Id, "RealtimeUpdated"); return Results.NoContent(); }

    private static async Task<IResult> AddMessage(Guid id, MessageBody body, ClaimsPrincipal user, HttpContext http, AppDbContext db, ManagementCompanyRequestAccessService access, ManagementCompanyRequestService service, ManagementCompanyRequestNotificationService notifications, ILogger<ManagementCompanyRequestNotificationService> logger, CancellationToken ct)
    {
        var actor = await access.RequireForRequestAsync(user, id, ct); var request = await Tracked(id, db, ct);
        var result = await service.AddMessageAsync(request, actor, body.Content, ct);
        await NotifyInteractionAsync(request, actor, result, notifications, logger, ct);
        await BroadcastSafeAsync(() => http.RequestServices.GetRequiredService<ManagementCompanyRequestRealtimeService>().BroadcastMessageAsync(request, result.Message, actor.Kind, actor.UserId, ct), logger, request.Id, "RealtimeMessage");
        await BroadcastSafeAsync(() => http.RequestServices.GetRequiredService<ManagementCompanyRequestRealtimeService>().BroadcastUpdatedAsync(request, ct), logger, request.Id, "RealtimeUpdated");
        var message = result.Message; return Results.Ok(new { message.Id, message.AuthorUserId, message.Content, message.CreatedAt });
    }

    private static async Task<IResult> UpdateMultipart(Guid id, HttpRequest http, ClaimsPrincipal user, AppDbContext db, ManagementCompanyRequestAccessService access, ManagementCompanyRequestService service, ManagementCompanyRequestNotificationService notifications, ILogger<ManagementCompanyRequestNotificationService> logger, CancellationToken ct)
    {
        var actor = await access.RequireForRequestAsync(user, id, ct); var request = await Tracked(id, db, ct); var (body, files, boleto) = await Multipart<UpdateRequestCommand>(http, ct);
        await service.UpdateAsync(request, actor, body, files, boleto, ct);
        await NotifySafeAsync(() => notifications.NotifyEditedAsync(request, ct), logger, request.Id, "Edited");
        return Results.NoContent();
    }

    private static async Task<IResult> Interact(Guid id, HttpRequest http, ClaimsPrincipal user, AppDbContext db, ManagementCompanyRequestAccessService access, ManagementCompanyRequestService service, ManagementCompanyRequestNotificationService notifications, ILogger<ManagementCompanyRequestNotificationService> logger, CancellationToken ct)
    {
        var actor = await access.RequireForRequestAsync(user, id, ct); var request = await Tracked(id, db, ct); var (body, files, _) = await Multipart<InteractionBody>(http, ct);
        var result = await service.InteractAsync(request, actor, body.Content, files, body.TargetStatus, ct);
        await NotifyInteractionAsync(request, actor, result, notifications, logger, ct);
        await BroadcastSafeAsync(() => http.HttpContext.RequestServices.GetRequiredService<ManagementCompanyRequestRealtimeService>().BroadcastMessageAsync(request, result.Message, actor.Kind, actor.UserId, ct), logger, request.Id, "RealtimeMessage");
        await BroadcastSafeAsync(() => http.HttpContext.RequestServices.GetRequiredService<ManagementCompanyRequestRealtimeService>().BroadcastUpdatedAsync(request, ct), logger, request.Id, "RealtimeUpdated");
        var message = result.Message; return Results.Ok(new { message.Id, message.AuthorUserId, message.Content, message.CreatedAt });
    }

    private static async Task<IResult> ChangeStatus(Guid id, StatusBody body, ClaimsPrincipal user, HttpContext http, AppDbContext db, ManagementCompanyRequestAccessService access, ManagementCompanyRequestService service, ManagementCompanyRequestNotificationService notifications, ILogger<ManagementCompanyRequestNotificationService> logger, CancellationToken ct)
    {
        var actor = await access.RequireForRequestAsync(user, id, ct); var request = await Tracked(id, db, ct);
        var history = await service.TransitionAsync(request, actor, body.Status, body.Reason, ct);
        if (history.EventType == ManagementCompanyRequestEventType.Completed) await NotifySafeAsync(() => notifications.NotifyCompletedAsync(request, ct), logger, request.Id, "Completed");
        await BroadcastSafeAsync(() => http.RequestServices.GetRequiredService<ManagementCompanyRequestRealtimeService>().BroadcastUpdatedAsync(request, ct), logger, request.Id, "RealtimeUpdated");
        return Results.NoContent();
    }

    private static async Task<IResult> CompletePayment(Guid id, HttpRequest http, ClaimsPrincipal user, AppDbContext db, ManagementCompanyRequestAccessService access, ManagementCompanyRequestService service, ManagementCompanyRequestNotificationService notifications, ILogger<ManagementCompanyRequestNotificationService> logger, CancellationToken ct)
    {
        var actor = await access.RequireForRequestAsync(user, id, ct); var request = await Tracked(id, db, ct);
        var (_, files, _) = await Multipart<CompletePaymentBody>(http, ct);
        var history = await service.CompletePaymentAsync(request, actor, files, null, ct);
        await NotifySafeAsync(() => notifications.NotifyCompletedAsync(request, ct), logger, request.Id, "Completed");
        await BroadcastSafeAsync(() => http.HttpContext.RequestServices.GetRequiredService<ManagementCompanyRequestRealtimeService>().BroadcastUpdatedAsync(request, ct), logger, request.Id, "RealtimeUpdated");
        return Results.NoContent();
    }

    private static async Task<IResult> Cancel(Guid id, CancelBody body, ClaimsPrincipal user, HttpContext http, AppDbContext db, ManagementCompanyRequestAccessService access, ManagementCompanyRequestService service, ManagementCompanyRequestNotificationService notifications, ILogger<ManagementCompanyRequestNotificationService> logger, CancellationToken ct)
    {
        var actor = await access.RequireForRequestAsync(user, id, ct); var request = await Tracked(id, db, ct);
        await service.CancelAsync(request, actor, body.Reason, ct);
        await NotifySafeAsync(() => notifications.NotifyCancelledAsync(request, body.Reason, ct, actor.Kind), logger, request.Id, "Cancelled");
        await BroadcastSafeAsync(() => http.RequestServices.GetRequiredService<ManagementCompanyRequestRealtimeService>().BroadcastUpdatedAsync(request, ct), logger, request.Id, "RealtimeUpdated");
        return Results.NoContent();
    }

    /// <summary>Runs an event-notification call without ever surfacing its failure to the HTTP caller: the request mutation already committed.</summary>
    private static async Task NotifySafeAsync(Func<Task> action, ILogger logger, Guid requestId, string eventName)
    {
        try { await action(); }
        catch (Exception exception) { logger.LogError(exception, "ManagementCompanyRequest notification dispatch failed. RequestId: {RequestId}; Event: {Event}.", requestId, eventName); }
    }

    private static async Task BroadcastSafeAsync(Func<Task> action, ILogger? logger, Guid requestId, string eventName)
    {
        try { await action(); }
        catch (Exception exception) { logger?.LogError(exception, "ManagementCompanyRequest realtime dispatch failed. RequestId: {RequestId}; Event: {Event}.", requestId, eventName); }
    }

    /// <summary>A message only produces a notification; it never mutates request state anymore.</summary>
    private static Task NotifyInteractionAsync(ManagementCompanyRequest request, ManagementCompanyRequestActor actor, ManagementCompanyRequestInteractionResult result, ManagementCompanyRequestNotificationService notifications, ILogger logger, CancellationToken ct)
        => NotifySafeAsync(() => notifications.NotifyMessageAsync(request, result.Message, actor.Kind, actor.UserId, ct), logger, request.Id, "Message");

    private static async Task<IResult> UploadAttachments(Guid id, HttpRequest http, ClaimsPrincipal user, AppDbContext db, ManagementCompanyRequestAccessService access, LocalFileStorage storage, CancellationToken ct)
    {
        var actor = await access.RequireForRequestAsync(user, id, ct);
        var request = await db.ManagementCompanyRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundAppException("Solicitação não encontrada.");
        if (request.IsTerminal) throw new ConflictAppException("Solicitações concluídas ou canceladas são somente leitura.");
        if (!http.HasFormContentType) throw new ValidationAppException("Envie os arquivos usando multipart/form-data.");
        var form = await http.ReadFormAsync(ct); var files = form.Files.GetFiles("files");
        var existing = await db.ManagementCompanyRequestAttachments.CountAsync(x => x.RequestId == id, ct);
        if (files.Count == 0 || existing + files.Count > AttachmentPolicy.MaximumFileCount) throw new ValidationAppException($"É permitido manter de 1 a {AttachmentPolicy.MaximumFileCount} anexos por solicitação.");
        Guid? messageId = Guid.TryParse(form["messageId"], out var parsed) ? parsed : null;
        if (messageId.HasValue && !await db.ManagementCompanyRequestMessages.AnyAsync(x => x.Id == messageId && x.RequestId == id, ct)) throw new ValidationAppException("A mensagem informada não pertence à solicitação.");
        var saved = new List<string>();
        try
        {
            foreach (var file in files)
            {
                var valid = AttachmentPolicy.Validate(file.FileName, file.Length, file.ContentType);
                if (valid.Error is not null) throw new ValidationAppException(valid.Error);
                var key = await storage.SaveManagementCompanyRequestAsync(id, file, valid.Extension!, ct);
                saved.Add(key);
                db.ManagementCompanyRequestAttachments.Add(new(id, actor.UserId, valid.Name!, key, valid.ContentType!, file.Length, messageId, messageId.HasValue ? ManagementCompanyRequestAttachmentPurpose.Message : ManagementCompanyRequestAttachmentPurpose.Request));
            }
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            foreach (var key in saved) storage.Delete(key);
            throw;
        }
        return Results.Ok(await db.ManagementCompanyRequestAttachments.AsNoTracking().Where(x => x.RequestId == id).Select(x => new { x.Id, x.MessageId, x.Purpose, x.OriginalFileName, x.ContentType, x.FileSize, x.CreatedAt }).ToListAsync(ct));
    }

    private static async Task<IResult> ListAttachments(Guid id, ClaimsPrincipal user, AppDbContext db, ManagementCompanyRequestAccessService access, CancellationToken ct)
    { await access.RequireForRequestAsync(user, id, ct); return Results.Ok(await db.ManagementCompanyRequestAttachments.AsNoTracking().Where(x => x.RequestId == id).Select(x => new { x.Id, x.MessageId, x.Purpose, x.OriginalFileName, x.ContentType, x.FileSize, x.CreatedAt }).ToListAsync(ct)); }

    private static async Task<IResult> DownloadAttachment(Guid attachmentId, ClaimsPrincipal user, HttpResponse response, AppDbContext db, ManagementCompanyRequestAccessService access, LocalFileStorage storage, CancellationToken ct)
    { var a = await db.ManagementCompanyRequestAttachments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attachmentId, ct) ?? throw new NotFoundAppException("Anexo não encontrado."); await access.RequireForRequestAsync(user, a.RequestId, ct); var stream = storage.OpenRead(a.StorageKey) ?? throw new NotFoundAppException("Arquivo não encontrado."); response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(a.OriginalFileName)}"; return Results.Stream(stream, a.ContentType); }

    private static Task<ManagementCompanyRequest> Tracked(Guid id, AppDbContext db, CancellationToken ct) => db.ManagementCompanyRequests.SingleOrDefaultAsync(x => x.Id == id, ct).ContinueWith(t => t.Result ?? throw new NotFoundAppException("Solicitação não encontrada."), ct);
    private static async Task<(T Body, IReadOnlyList<IFormFile> Files, IReadOnlyList<IFormFile> BoletoFiles)> Multipart<T>(HttpRequest http, CancellationToken ct) { if (!http.HasFormContentType) throw new ValidationAppException("Envie os dados usando multipart/form-data."); var form = await http.ReadFormAsync(ct); var json = form["payload"].ToString(); if (string.IsNullOrWhiteSpace(json)) throw new ValidationAppException("O campo payload é obrigatório."); T? body; try { body = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)); } catch (JsonException) { throw new ValidationAppException("O payload informado é inválido."); } if (body is null) throw new ValidationAppException("O payload informado é inválido."); return (body, form.Files.GetFiles("files"), form.Files.GetFiles("boleto")); }

    private static async Task<object> ToDetail(Guid id, AppDbContext db, CancellationToken ct)
    {
        var r = await db.ManagementCompanyRequests.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        var condominiumName = await db.Condominiums.Where(x => x.Id == r.CondominiumId).Select(x => x.Name).SingleAsync(ct);
        var managementCompanyName = await db.ManagementCompanies.Where(x => x.Id == r.ManagementCompanyId).Select(x => x.Name).SingleAsync(ct);
        var condominium = await db.Condominiums.AsNoTracking().Where(x => x.Id == r.CondominiumId).Select(x => new { x.Name, x.Address, x.City, x.State }).SingleAsync(ct);
        var managerRoles = await db.CondominiumMemberships.AsNoTracking().Where(m => m.CondominiumId == r.CondominiumId && m.IsActive && m.EndedAt == null).Join(db.CondominiumMembershipRoles.AsNoTracking().Where(role => role.IsActive && role.RevokedAt == null && (role.Role == CondominiumRole.Manager || role.Role == CondominiumRole.SubManager)), m => m.Id, role => role.CondominiumMembershipId, (m, role) => new { m.UserId, role.Role }).Join(db.Users.AsNoTracking().Where(u => u.IsActive), x => x.UserId, u => u.Id, (x, u) => new { Id = u.Id, u.FullName, x.Role }).ToListAsync(ct);
        var managers = managerRoles.GroupBy(x => x.Id).Select(group => group.OrderBy(x => x.Role == CondominiumRole.Manager ? 0 : 1).First()).OrderBy(x => x.Role == CondominiumRole.Manager ? 0 : 1).ToArray();
        var creator = await db.Users.AsNoTracking().Where(x => x.Id == r.CreatedByUserId).Select(x => new { x.Id, x.FullName }).SingleAsync(ct);
        var creatorRole = await db.CondominiumMemberships.AsNoTracking()
            .Where(m => m.CondominiumId == r.CondominiumId && m.UserId == r.CreatedByUserId && m.JoinedAt <= r.CreatedAt && (m.EndedAt == null || m.EndedAt >= r.CreatedAt))
            .Join(db.CondominiumMembershipRoles.AsNoTracking().Where(role => (role.Role == CondominiumRole.Manager || role.Role == CondominiumRole.SubManager) && role.GrantedAt <= r.CreatedAt && (role.RevokedAt == null || role.RevokedAt >= r.CreatedAt)), m => m.Id, role => role.CondominiumMembershipId, (m, role) => role.Role)
            .OrderBy(role => role == CondominiumRole.Manager ? 0 : 1).Cast<CondominiumRole?>().FirstOrDefaultAsync(ct);
        var fine = await db.ManagementCompanyFineRequests.AsNoTracking().Where(x => x.RequestId == id).Select(x => new { x.UnitId, Unit = db.Units.Where(u => u.Id == x.UnitId).Select(u => u.Identifier).FirstOrDefault(), Block = db.Units.Where(u => u.Id == x.UnitId).Select(u => u.BlockId == null ? null : db.CondominiumBlocks.Where(b => b.Id == u.BlockId).Select(b => b.Identifier).FirstOrDefault()).FirstOrDefault(), x.Nature, x.Description, x.OccurrenceDate, x.Value, x.ValueNotDefined }).SingleOrDefaultAsync(ct);
        var payment = await db.ManagementCompanyPaymentRequests.AsNoTracking().Where(x => x.RequestId == id).Select(x => new { x.Nature, x.Value, x.EventDate, x.DueDate, x.IsReimbursement, x.Notes, x.BeneficiaryUserId, x.BeneficiaryName, x.PixKeyType, x.PixKey, x.ThirdPartyIdentification, x.ThirdPartyForm, x.ThirdPartyPixKey, x.ThirdPartyBank, x.ThirdPartyAgency, x.ThirdPartyAccount }).SingleOrDefaultAsync(ct);
        var question = await db.ManagementCompanyGeneralQuestionRequests.AsNoTracking().Where(x => x.RequestId == id).Select(x => new { x.Theme }).SingleOrDefaultAsync(ct);
        var messages = await db.ManagementCompanyRequestMessages.AsNoTracking().Where(x => x.RequestId == id).OrderBy(x => x.CreatedAt).Select(x => new { x.Id, x.AuthorUserId, AuthorName = db.Users.Where(u => u.Id == x.AuthorUserId).Select(u => u.FullName).FirstOrDefault() ?? "Usuário", AuthorRole = db.ManagementCompanyEmployees.Where(e => e.UserId == x.AuthorUserId && e.ManagementCompanyId == r.ManagementCompanyId).Select(e => e.AccessType == ManagementCompanyAccessType.Department ? e.JobTitle : e.JobTitle + " · " + managementCompanyName).FirstOrDefault() ?? (db.CondominiumMemberships.Where(m => m.UserId == x.AuthorUserId && m.CondominiumId == r.CondominiumId).Join(db.CondominiumMembershipRoles.Where(role => role.IsActive && role.RevokedAt == null), m => m.Id, role => role.CondominiumMembershipId, (m, role) => role.Role).Any(role => role == CondominiumRole.SubManager) ? "Subsíndico" : "Síndico"), x.Content, x.CreatedAt }).ToListAsync(ct);
        var history = await db.ManagementCompanyRequestHistories.AsNoTracking().Where(x => x.RequestId == id).OrderBy(x => x.CreatedAt).Select(x => new { x.Id, x.EventType, x.PreviousStatus, x.NewStatus, x.ChangedByUserId, ChangedByName = db.Users.Where(u => u.Id == x.ChangedByUserId).Select(u => u.FullName).FirstOrDefault(), x.Reason, x.CreatedAt }).ToListAsync(ct);
        var cancellation = history.LastOrDefault(x => x.EventType == ManagementCompanyRequestEventType.Cancelled);
        string? cancellationOrigin = null;
        if (cancellation is not null)
        {
            var managerRole = await db.CondominiumMembershipRoles.Where(role => role.Role == CondominiumRole.Manager || role.Role == CondominiumRole.SubManager).Join(db.CondominiumMemberships, role => role.CondominiumMembershipId, membership => membership.Id, (role, membership) => new { role, membership }).Where(x => x.membership.CondominiumId == r.CondominiumId && x.membership.UserId == cancellation.ChangedByUserId && x.role.GrantedAt <= cancellation.CreatedAt && (x.role.RevokedAt == null || x.role.RevokedAt >= cancellation.CreatedAt)).Select(x => x.role.Role).FirstOrDefaultAsync(ct);
            cancellationOrigin = managerRole == CondominiumRole.SubManager ? "SubManager" : managerRole == CondominiumRole.Manager ? "Manager" : await db.ManagementCompanyEmployees.AnyAsync(e => e.ManagementCompanyId == r.ManagementCompanyId && e.UserId == cancellation.ChangedByUserId, ct) ? "ManagementCompany" : null;
        }
        var attachments = await db.ManagementCompanyRequestAttachments.AsNoTracking().Where(x => x.RequestId == id).OrderBy(x => x.CreatedAt).Select(x => new { x.Id, x.MessageId, x.Purpose, x.OriginalFileName, x.ContentType, x.FileSize, x.CreatedAt }).ToListAsync(ct);
        return new
        {
            r.Id, r.FriendlyIdentifier, r.CondominiumId, condominiumName, r.ManagementCompanyId, managementCompanyName, r.CategoryId, r.CreatedByUserId, r.Type, r.Status, r.CreatedAt, r.UpdatedAt, r.AcknowledgedAt, r.AcknowledgedByUserId, r.CompletedAt, r.CompletedByUserId, r.CancelledAt, r.CancelledByUserId, r.CancellationReason, cancellationOrigin,
            requester = new { creator.Id, creator.FullName, Role = creatorRole?.ToString() },
            condominium = new { condominium.Name, condominium.Address, condominium.City, condominium.State, managers },
            fine, payment, question, messages, history, attachments
        };
    }

    public sealed record MessageBody(string Content);
    public sealed record StatusBody(ManagementCompanyRequestStatus Status, string? Reason);
    public sealed record CancelBody(string Reason);
    public sealed record InteractionBody(string Content, ManagementCompanyRequestStatus? TargetStatus);
    public sealed record CompletePaymentBody();
}

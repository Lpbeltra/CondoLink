using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.RequestAttachments;

public static class RequestAttachmentEndpoints
{
    private const int MaximumFileCount = 6;
    private const long MaximumFileSize = 15 * 1024 * 1024;
    private const long MaximumRequestSize = 96 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string[]> AllowedFiles =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = ["image/jpeg"],
            [".jpeg"] = ["image/jpeg"],
            [".png"] = ["image/png"],
            [".webp"] = ["image/webp"],
            [".pdf"] = ["application/pdf"]
        };

    public static IEndpointRouteBuilder MapRequestAttachments(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/requests/{requestId:guid}/attachments", UploadAsync)
            .RequireAuthorization()
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestSize));
        endpoints.MapGet("/requests/{requestId:guid}/attachments", ListAsync).RequireAuthorization();
        endpoints.MapGet("/request-attachments/{attachmentId:guid}/content", ContentAsync).RequireAuthorization();
        endpoints.MapDelete("/request-attachments/{attachmentId:guid}", DeleteAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> UploadAsync(Guid requestId, HttpRequest request,
        ClaimsPrincipal principal, AppDbContext dbContext, LocalFileStorage storage,
        CancellationToken cancellationToken)
    {
        var access = await CheckAccessAsync(requestId, principal, dbContext, cancellationToken);
        if (access.Error is not null) return access.Error;
        if (access.Status == RequestStatus.Cancelled)
            return Results.Conflict(new { error = "Solicitações canceladas não podem receber anexos." });
        if (!request.HasFormContentType)
            return Results.BadRequest(new { error = "Envie os arquivos usando o formato multipart/form-data." });

        IFormCollection form;
        try { form = await request.ReadFormAsync(cancellationToken); }
        catch (InvalidDataException)
        {
            return Results.BadRequest(new { error = "Não foi possível ler os arquivos enviados." });
        }
        var files = form.Files.GetFiles("files");
        if (files.Count == 0)
            return Results.BadRequest(new { error = "Selecione ao menos um arquivo." });
        if (files.Count > MaximumFileCount)
            return Results.BadRequest(new { error = "É permitido enviar no máximo 6 arquivos." });

        var validated = new List<(IFormFile File, string Name, string Extension, string ContentType)>();
        foreach (var file in files)
        {
            var name = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(name);
            var contentType = file.ContentType.Split(';', 2)[0].Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
                return Results.BadRequest(new { error = "O nome do arquivo é inválido ou possui mais de 255 caracteres." });
            if (file.Length <= 0)
                return Results.BadRequest(new { error = $"O arquivo “{name}” está vazio." });
            if (file.Length > MaximumFileSize)
                return Results.BadRequest(new { error = "Cada arquivo pode possuir no máximo 15 MB." });
            if (!AllowedFiles.TryGetValue(extension, out var contentTypes)
                || !contentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "Formato não suportado. Envie somente JPG, PNG, WebP ou PDF." });
            validated.Add((file, name, extension.ToLowerInvariant(), contentType.ToLowerInvariant()));
        }

        var savedKeys = new List<string>();
        try
        {
            var attachments = new List<RequestAttachment>();
            foreach (var item in validated)
            {
                var key = await storage.SaveAsync(requestId, item.File, item.Extension, cancellationToken);
                savedKeys.Add(key);
                attachments.Add(new RequestAttachment(requestId, access.UserId, item.Name, key,
                    item.ContentType, item.File.Length));
            }
            dbContext.RequestAttachments.AddRange(attachments);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Created($"/requests/{requestId}/attachments",
                attachments.Select(x => ToResponse(x, access.FullName)).ToArray());
        }
        catch
        {
            foreach (var key in savedKeys) storage.Delete(key);
            throw;
        }
    }

    private static async Task<IResult> ListAsync(Guid requestId, ClaimsPrincipal principal,
        AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var access = await CheckAccessAsync(requestId, principal, dbContext, cancellationToken);
        if (access.Error is not null) return access.Error;

        var rows = await dbContext.RequestAttachments.AsNoTracking()
            .Where(x => x.RequestId == requestId)
            .Join(dbContext.Set<ApplicationUser>().AsNoTracking(), x => x.UploadedByUserId, u => u.Id,
                (x, u) => new { Attachment = x, u.FullName })
            .OrderBy(x => x.Attachment.CreatedAt).ThenBy(x => x.Attachment.Id)
            .ToListAsync(cancellationToken);
        return Results.Ok(rows.Select(x => ToResponse(x.Attachment, x.FullName)).ToArray());
    }

    private static async Task<IResult> ContentAsync(Guid attachmentId, ClaimsPrincipal principal,
        HttpResponse response, AppDbContext dbContext, LocalFileStorage storage,
        CancellationToken cancellationToken)
    {
        var attachment = await dbContext.RequestAttachments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == attachmentId, cancellationToken);
        if (attachment is null) return Results.NotFound(new { error = "Anexo não encontrado." });
        var access = await CheckAccessAsync(attachment.RequestId, principal, dbContext, cancellationToken);
        if (access.Error is not null) return access.Error;

        FileStream? stream;
        try { stream = storage.OpenRead(attachment.StorageKey); }
        catch (InvalidOperationException) { stream = null; }
        if (stream is null) return Results.NotFound(new { error = "O arquivo do anexo não foi encontrado." });
        response.Headers.ContentDisposition = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(attachment.OriginalFileName)}";
        response.Headers.CacheControl = "no-store";
        return Results.File(stream, attachment.ContentType, enableRangeProcessing: true);
    }

    private static async Task<IResult> DeleteAsync(Guid attachmentId, ClaimsPrincipal principal,
        AppDbContext dbContext, LocalFileStorage storage, CancellationToken cancellationToken)
    {
        var attachment = await dbContext.RequestAttachments
            .SingleOrDefaultAsync(x => x.Id == attachmentId, cancellationToken);
        if (attachment is null) return Results.NotFound(new { error = "Anexo não encontrado." });

        var access = await CheckAccessAsync(attachment.RequestId, principal, dbContext, cancellationToken);
        if (access.Error is not null) return access.Error;

        dbContext.RequestAttachments.Remove(attachment);
        await dbContext.SaveChangesAsync(cancellationToken);
        storage.Delete(attachment.StorageKey);
        return Results.NoContent();
    }

    private static Response ToResponse(RequestAttachment x, string fullName) => new(x.Id, x.RequestId,
        x.OriginalFileName, x.ContentType, x.FileSize, new UploadedByResponse(x.UploadedByUserId, fullName),
        x.CreatedAt, $"/request-attachments/{x.Id}/content");

    private static async Task<AccessCheck> CheckAccessAsync(Guid requestId, ClaimsPrincipal principal,
        AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId))
            return new(Guid.Empty, "", default, Results.Json(new { error = "Invalid authenticated user." }, statusCode: 401));
        var user = await dbContext.Set<ApplicationUser>().AsNoTracking().Where(x => x.Id == userId)
            .Select(x => new { x.IsActive, x.FullName }).SingleOrDefaultAsync(cancellationToken);
        if (user is null)
            return new(userId, "", default, Results.Json(new { error = "Authenticated user was not found." }, statusCode: 401));
        if (!user.IsActive)
            return new(userId, user.FullName, default, Results.Json(new { error = "User account is inactive." }, statusCode: 403));
        var target = await dbContext.Requests.AsNoTracking().Where(x => x.Id == requestId)
            .Select(x => new { x.AuthorUserId, x.CondominiumId, x.Status }).SingleOrDefaultAsync(cancellationToken);
        if (target is null)
            return new(userId, user.FullName, default, Results.NotFound(new { error = "Request not found." }));
        if (target.AuthorUserId != userId)
        {
            var manager = await dbContext.CondominiumMemberships.AsNoTracking()
                .Where(x => x.UserId == userId && x.CondominiumId == target.CondominiumId && x.IsActive && x.EndedAt == null)
                .Join(dbContext.CondominiumMembershipRoles.AsNoTracking().Where(x => x.Role == CondominiumRole.Manager && x.IsActive && x.RevokedAt == null),
                    x => x.Id, x => x.CondominiumMembershipId, (_, _) => true).AnyAsync(cancellationToken);
            if (!manager)
                return new(userId, user.FullName, target.Status, Results.Json(new { error = "You do not have access to this request." }, statusCode: 403));
        }
        return new(userId, user.FullName, target.Status, null);
    }

    private sealed record AccessCheck(Guid UserId, string FullName, RequestStatus Status, IResult? Error);
    public sealed record UploadedByResponse(Guid Id, string FullName);
    public sealed record Response(Guid Id, Guid RequestId, string OriginalFileName, string ContentType,
        long FileSize, UploadedByResponse UploadedBy, DateTime CreatedAt, string ContentUrl);
}
